# REST API для управления событиями

Для запуска требуется .NET SDK 10 версии;

Проект представляет собой API для управления событиями.
Архитектура - (Domain - Application - Infrastructure - API):

- Domain содержит ядро приложения - неизменяемые сущности;
- Application - бизнес-логика + контракты системы;
- Infrastructure - связь с хранилищем данных и реализация CRUD-операций;
- API - точка входа в приложение, содержащая контроллеры, DI(стартовать приложение нужно именно отсюда).

Связи проекта:

API -> Application -> Domain;

API -> Infrastructure -> Application -> Domain.

## Запуск (в папке EventsService)

build при желании выполнять в EventsService папке - сборка всего solution

```bash
dotnet restore
dotnet run --project EventsService.Api
```

После запуска Swagger UI доступен по адресу, указанному в консоли - `http://localhost:5000/swagger`.

## API

Базовый путь: `/events`

| Метод  | Путь           | Описание                                  | Успех | Не найдено |
| ------ | -------------- | ----------------------------------------- | ----- | ---------- |
| GET    | `/events`      | Список событий (с фильтрами и пагинацией) | 200   | —          |
| GET    | `/events/{id}` | Событие по Id                             | 200   | 404        |
| POST   | `/events`      | Создать событие                           | 201   | —          |
| PUT    | `/events/{id}` | Обновить событие целиком                  | 200   | 404        |
| DELETE | `/events/{id}` | Удалить событие                           | 204   | 404        |

### Параметры фильтрации `GET /events`

Все параметры передаются через query string и являются необязательными:

| Параметр   | Тип         | По умолчанию | Описание                                                                |
| ---------- | ----------- | ------------ | ----------------------------------------------------------------------- |
| `title`    | `string?`   | `null`       | Фильтр по названию - регистронезависимое вхождение подстроки в `Title`. |
| `from`     | `DateTime?` | `null`       | Оставить только события, у которых `StartAt >= from`.                   |
| `to`       | `DateTime?` | `null`       | Оставить только события, у которых `EndAt <= to`.                       |
| `page`     | `int`       | `1`          | Номер страницы.                                                         |
| `pageSize` | `int`       | `10`         | Размер страницы.                                                        |

Фильтры можно комбинировать. Ответ — `PaginatedResult<EventResponseDto>`:

```json
{
  "items": [
    /* EventResponseDto[] на текущей странице */
  ],
  "totalCount": /* общее количество элементов, прошедших фильтрацию (без учёта пагинации) */,
  "page": /* номер текущей страницы (из запроса) */,
  "pageSize": /* запрошенный размер страницы (из запроса) */,
  "itemsCount": /* фактическое количество элементов в items - на последней странице может быть меньше pageSize */
}
```

Пример: `GET /events?title=.NET&from=2026-09-01T00:00:00Z&to=2026-09-30T00:00:00Z&page=2&pageSize=5`.

### Модель Event / EventResponseDTO

| Поле           | Тип      | Обязательное                               |
| -------------- | -------- | ------------------------------------------- |
| Id             | Guid     | генерируется сервером                       |
| Title          | string   | да                                           |
| Description    | string   | нет                                          |
| StartAt        | DateTime | да                                           |
| EndAt          | DateTime | да                                           |
| TotalSeats     | int      | да, > 0                                      |
| AvailableSeats | int      | нет, равно `TotalSeats` при создании события |

### DTO Create/UpdateEventDto

| Поле        | Тип      | Обязательное |
| ----------- | -------- | ------------ |
| Title       | string   | да           |
| Description | string   | нет          |
| StartAt     | DateTime | да           |
| EndAt       | DateTime | да           |
| TotalSeats  | int      | да, > 0      |

### Валидация (POST / PUT)

- `Title`, `StartAt`, `EndAt`, `TotalSeats` обязательны.
- `EndAt` должен быть позже `StartAt` и `StartAt` > текущее время.
- `TotalSeats` должен быть больше нуля.
- Нарушение - `400 Bad Request` с деталями ошибок.

## Бронирования (Bookings)

Бронь создаётся так: `POST` синхронно и атомарно резервирует место у события (`AvailableSeats -= 1`) и мгновенно создаёт бронь в статусе `Pending`, возвращая `202 Accepted`. Фактическое подтверждение/отклонение брони выполняется асинхронно фоновым сервисом.

| Метод | Путь                | Описание                      | Успех | Не найдено | Нет мест |
| ----- | ------------------- | ------------------------------ | ----- | ---------- | -------- |
| POST  | `/events/{id}/book` | Создать бронь для события     | 202   | 404        | 409      |
| GET   | `/bookings/{id}`    | Получить текущий статус брони | 200   | 404        | —        |

`POST /events/{id}/book`:

- если события с `id` не существует - `404`;
- если у события не осталось свободных мест (`AvailableSeats == 0`) - `409 Conflict` (`NoAvailableSeatsException`);
- иначе место резервируется и бронь создаётся сразу в статусе `Pending`;
- в теле ответа - `BookingResponseDto` созданной брони;
- в заголовке `Location` - ссылка на ресурс брони: `/bookings/{bookingId}`.

### Защита от овербукинга (`InMemoryBookingService`)

Проверка свободных мест и создание брони - не атомарны сами по себе (это два отдельных обращения к разным хранилищам), поэтому при параллельных запросах на одно и то же событие без синхронизации возможен overbooking. `InMemoryBookingService.CreateBookingAsync` оборачивает весь критический участок (`GetEventAsync` → `TryReserveSeats` → `UpdateEventAsync` → `CreateBookingAsync`) в `lock`, гарантируя, что проверка доступности мест и их резервирование выполняются как единая атомарная операция - конкурентные запросы обрабатываются строго по очереди, и ни один из них не увидит "устаревшее" значение `AvailableSeats`.

### Примитивы синхронизации: какие и зачем

В проекте используются два разных примитива - в разных местах и по разным причинам:

| Примитив                | Где                                                | Почему именно он                                                                                                                                                                                                       |
| ------------------------ | --------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `lock` (Monitor)         | `InMemoryBookingService.CreateBookingAsync`          | Весь критический участок выполняется синхронно (обращения к in-memory репозиториям без реального I/O и без `await` внутри `lock`), поэтому подходит обычный `lock` - он дешевле `SemaphoreSlim` и не требует `async`. |
| `SemaphoreSlim(1, 1)`     | `BookingProcessingBackgroundService.ProcessBookingAsync` | Внутри критической секции есть `await` (запись в репозитории через `async`-методы), а `lock` не позволяет `await` внутри заблокированного блока (`CS1996`). `SemaphoreSlim` - асинхронный аналог мьютекса: `WaitAsync()`/`Release()` можно безопасно вызывать вокруг `await`-кода.                          |

Оба примитива защищают одно и то же по смыслу - консистентность записи в общие in-memory хранилища (`List<Event>`, `List<Booking>`) при параллельных обращениях, - но выбраны с учётом того, содержит ли защищаемый код `await` или нет.

### Модель Booking / BookingResponseDto

| Поле        | Тип           | Обязательное                                |
| ----------- | ------------- | ------------------------------------------- |
| Id          | Guid          | генерируется сервером                       |
| EventId     | Guid          | да - Id события, к которому относится бронь |
| Status      | BookingStatus | да, по умолчанию `Pending`                  |
| CreatedAt   | DateTime      | да, проставляется при создании              |
| ProcessedAt | DateTime?     | заполняется после фоновой обработки         |

### Статусы (`BookingStatus`)

| Статус      | Значение | Описание                                         |
| ----------- | -------- | ------------------------------------------------ |
| `Pending`   | 0        | бронь создана, ожидает фоновой обработки         |
| `Confirmed` | 1        | бронь подтверждена (обработка прошла без ошибок) |
| `Rejected`  | 2        | бронь отклонена (при обработке возникла ошибка)  |

Данные о бронированиях хранятся в памяти приложения, аналогично событиям (`EventsService.Infrastructure/Repositories/InMemoryBookingRepository`).

### Фоновая обработка (`BookingProcessingBackgroundService`)

Реализована как `BackgroundService` (`EventsService.Infrastructure/BackgroundServices`), зарегистрирована в DI через `AddHostedService`:

- раз в `PollingInterval` (10 секунд) опрашивает хранилище броней и выбирает все брони в статусе `Pending`;
- все найденные брони обрабатываются параллельно - `ProcessBookingAsync` запускается для каждой брони отдельным `Task`, и итерация ждёт их завершения через `Task.WhenAll`;
- для каждой брони сначала имитируется обращение к внешней системе через `Task.Delay(ProcessingDelay)` (2 секунды) - так задержки разных броней выполняются параллельно, а не суммируются;
- запись в хранилища защищена от гонок через `SemaphoreSlim(1, 1)`;
- если к моменту обработки событие уже удалено - бронь переводится в `Rejected` и обработка завершается с логом `Warning`;
- если событие найдено и обработка прошла без ошибок - бронь переводится в `Confirmed`;
- если во время обработки возникло непредвиденное исключение - бронь переводится в `Rejected`, а занятое ею место возвращается в пул через `Event.ReleaseSeats()`;
- в обоих терминальных статусах заполняется `ProcessedAt`;
- корректно обрабатывает отмену при остановке хоста и ошибки на уровне отдельной брони/итерации, не прерывая работу сервиса;
- ход обработки логируется через `ILogger<BookingProcessingBackgroundService>`; лог "Processing booking {BookingId}" пишется в начале `ProcessBookingAsync` для каждой брони до её `await`, поэтому в выводе видно, что несколько броней стартуют одновременно, до завершения обработки любой из них.

### Пример сценария использования

```bash
# 1. Создать событие на 1 место
curl -X POST http://localhost:5000/events \
  -H "Content-Type: application/json" \
  -d '{"title":"Конференция .NET","startAt":"2026-12-01T10:00:00Z","endAt":"2026-12-01T12:00:00Z","totalSeats":1}'
# -> 201 Created, тело содержит "id" события, "totalSeats":1, "availableSeats":1

# 2. Забронировать место на событии
curl -i -X POST http://localhost:5000/events/{eventId}/book
# -> 202 Accepted
# -> Location: /bookings/{bookingId}
# -> тело: {"id":"...","eventId":"...","status":0 (Pending),"createdAt":"...","processedAt":null}

# 3. Сразу проверить статус - бронь ещё не обработана
curl http://localhost:5000/bookings/{bookingId}
# -> status: 0 (Pending), processedAt: null

# 4. Повторная попытка забронировать то же событие - мест больше нет
curl -i -X POST http://localhost:5000/events/{eventId}/book
# -> 409 Conflict

# 5. Подождать несколько секунд (фоновый сервис опрашивает раз в PollingInterval=10с + ProcessingDelay=2с имитация обработки)
sleep 12
curl http://localhost:5000/bookings/{bookingId}
# -> status: 1 (Confirmed) или 2 (Rejected), processedAt заполнен
# если Rejected (например, событие удалили до обработки) - AvailableSeats события восстановится на 1
```

### Пример сценария с овербукингом

Показывает, что при параллельных запросах на событие с ограниченным числом мест успешных броней ровно столько же, сколько мест, а не больше:

```bash
# 1. Создать событие на 3 места
eventId=$(curl -s -X POST http://localhost:5000/events \
  -H "Content-Type: application/json" \
  -d '{"title":"Конференция .NET","startAt":"2026-12-01T10:00:00Z","endAt":"2026-12-01T12:00:00Z","totalSeats":3}' \
  | jq -r '.id')

# 2. Отправить 10 конкурентных запросов на бронирование (параллельно, в фоне)
for i in $(seq 1 10); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "http://localhost:5000/events/${eventId}/book" &
done
wait
# -> в выводе ровно 3 строки "202" и 7 строк "409" (порядок непредсказуем из-за конкуренции)

# 3. Проверить итоговое количество мест
curl -s http://localhost:5000/events/${eventId} | jq '.availableSeats'
# -> 0 (ушли ровно 3 места, не меньше и не больше - lock в InMemoryBookingService не допустил overbooking)
```

## Формат ответа при ошибках

Все ошибки обрабатываются глобальным `GlobalExceptionHandler` (`EventsService.Api/Exceptions`) и возвращаются в формате **RFC ProblemDetails** (`application/problem+json`):

- `NotFoundException` (событие/бронь не найдены) - **404**
- `ValidationException` (доменная/DTO-валидация, включая `TotalSeats <= 0`) - **400**
- `NoAvailableSeatsException` (нет свободных мест у события) - **409**
- Необработанное исключение - **500**

**404 Not Found:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Сущность с Id: {id} не найдена"
}
```

**400 Bad Request** (если у `ValidationException` указано конкретное поле, добавляется `errors`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "EndAt должен быть позже StartAt",
  "errors": {
    "endAt": ["EndAt должен быть позже StartAt"]
  }
}
```

**409 Conflict** (`POST /events/{id}/book` при `AvailableSeats == 0`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "No available seats for this event"
}
```

**500 Internal Server Error:**

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An internal server error has occurred. Please try again later."
}
```

В окружении `Development` в `detail` для 500 попадает исходное сообщение исключения, а не общий текст.

## Тесты

Юнит-тесты (xUnit + Moq) находятся в проекте `EventsService.Tests`:

- `InMemoryEventServiceTests` - покрывает `InMemoryEventService`: успешные и неуспешные сценарии CRUD, фильтрацию, пагинацию и граничные случаи;
- `InMemoryBookingServiceTests` - покрывает `InMemoryBookingService` на моках репозиториев: создание брони для существующего/несуществующего/удалённого события, уникальность Id при нескольких бронях на одно событие, получение брони по Id (включая отражение смены статуса после `Confirm`/`Reject`) и получение по несуществующему Id;
- `BookingSeatManagementTests` - покрывает логику мест и защиту от овербукинга поверх реальных `InMemoryEventRepository`/`InMemoryBookingRepository`:
  - уменьшение `AvailableSeats` после успешной брони, создание броней до исчерпания лимита с уникальными Id, `NoAvailableSeatsException` после исчерпания мест, `NotFoundException` для несуществующего события;
  - смена статуса брони через `Confirm()`/`Reject()` (статус + `ProcessedAt`), восстановление `AvailableSeats` и возможность новой брони после `Reject()` + `ReleaseSeats()`;
  - конкурентность: 20 параллельных запросов (`Task.Run` + `Task.WhenAll`, реальный параллелизм на пуле потоков) на событие с 5 местами - ровно 5 успехов и 15 `NoAvailableSeatsException`, `AvailableSeats == 0`; 10 параллельных запросов на событие с 10 местами - все 10 броней получают уникальные Id.

Запуск всех тестов (из папки `EventsService`):

```bash
dotnet test EventsService.Tests/EventsService.Tests.csproj
```

Либо запустить тесты для всего solution:

```bash
dotnet test
```

## Стек

ASP.NET Core(.NET 10), Swashbuckle (Swagger UI), DI-контейнер встроенный в ASP.NET Core, xUnit + Moq.
