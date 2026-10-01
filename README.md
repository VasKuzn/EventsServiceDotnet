# REST API для управления событиями

Для запуска требуется:

- .NET SDK 10 версии;
- **PostgreSQL** - приложение хранит события и бронирования в PostgreSQL и без доступной базы не запустится.

Проект представляет собой API для управления событиями.
Архитектура - (Domain - Application - Infrastructure - API):

- Domain содержит ядро приложения - неизменяемые сущности;
- Application - бизнес-логика + контракты системы;
- Infrastructure - связь с хранилищем данных (EF Core + PostgreSQL, `AppDbContext`) и реализация CRUD-операций;
- API - точка входа в приложение, содержащая контроллеры, DI(стартовать приложение нужно именно отсюда).

Связи проекта:

API -> Application -> Domain;

API -> Infrastructure -> Application -> Domain.

## База данных (PostgreSQL)

Строка подключения берётся из `ConnectionStrings:DefaultConnection` в `EventsService.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=EventApi;Username=postgres;Password=postgres"
}
```

Чтобы подключиться к своему серверу, измените значения `Host`, `Port`, `Database`, `Username` и `Password`. Значение можно переопределить, не меняя файл, через переменную окружения:

```bash
# bash
export ConnectionStrings__DefaultConnection="Host=myhost;Port=5432;Database=EventApi;Username=user;Password=secret"
```

```powershell
# PowerShell
$env:ConnectionStrings__DefaultConnection = "Host=myhost;Port=5432;Database=EventApi;Username=user;Password=secret"
```

Схема БД создаётся автоматически при запуске приложения: в `Program.cs` после `builder.Build()` вызывается `db.Database.EnsureCreated()`. Метод создаёт базу данных и таблицы `Event` и `Booking`, если их ещё нет, а при повторных запусках ничего не делает. Миграции не используются: если схема сущностей изменится, существующую базу нужно удалить, чтобы `EnsureCreated` создал её заново.

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

| Поле           | Тип      | Обязательное                                 |
| -------------- | -------- | -------------------------------------------- |
| Id             | Guid     | генерируется сервером                        |
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
| ----- | ------------------- | ----------------------------- | ----- | ---------- | -------- |
| POST  | `/events/{id}/book` | Создать бронь для события     | 202   | 404        | 409      |
| GET   | `/bookings/{id}`    | Получить текущий статус брони | 200   | 404        | —        |

`POST /events/{id}/book`:

- если события с `id` не существует - `404`;
- если у события не осталось свободных мест (`AvailableSeats == 0`) - `409 Conflict` (`NoAvailableSeatsException`);
- иначе место резервируется и бронь создаётся сразу в статусе `Pending`;
- в теле ответа - `BookingResponseDto` созданной брони;
- в заголовке `Location` - ссылка на ресурс брони: `/bookings/{bookingId}`.

### Защита от овербукинга (`BookingService`)

При параллельных запросах на одно и то же событие без синхронизации возможен overbooking: два запроса, каждый со своим `DbContext`, могут прочитать одно и то же значение `AvailableSeats` и оба его уменьшить. `BookingService.CreateBookingAsync` оборачивает весь критический участок (`GetEventAsync` → `TryReserveSeats` → `UpdateEventAsync` → `CreateBookingAsync`) в `SemaphoreSlim(1, 1)`, гарантируя, что проверка доступности мест и их резервирование выполняются как единая атомарная операция - конкурентные запросы обрабатываются строго по очереди, и ни один из них не увидит "устаревшее" значение `AvailableSeats`.

Важная деталь про DI: `IBookingService`, репозитории и `AppDbContext` зарегистрированы как `Scoped` - у каждого HTTP-запроса свой экземпляр сервиса и свой `DbContext`. Поэтому семафор в `BookingService` объявлен `static`: экземпляр семафора, привязанный к scope, не защищал бы запросы друг от друга. Такая защита работает в пределах одного процесса приложения; при запуске нескольких экземпляров сервиса понадобится блокировка или оптимистичная конкуренция на уровне БД.

### Примитивы синхронизации: какие и зачем

| Примитив                      | Где                                         | Что защищает                                                                                                                              |
| ----------------------------- | ------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `static SemaphoreSlim(1, 1)`  | `BookingService.CreateBookingAsync`         | Атомарность проверки `AvailableSeats` и создания брони при параллельных `POST /events/{id}/book` на одно событие - защита от overbooking. |

В `BookingProcessingBackgroundService` синхронизация не нужна: каждая бронь обрабатывается в своём scope со своим `DbContext`, а общих изменяемых объектов между задачами нет.

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

Данные о бронированиях и событиях хранятся в PostgreSQL (таблицы `Booking` и `Event`) и сохраняются между перезапусками приложения (`EventsService.Infrastructure/Repositories/BookingRepository` и `EventRepository`).

### Фоновая обработка (`BookingProcessingBackgroundService`)

Реализована как `BackgroundService` (`EventsService.Infrastructure/BackgroundServices`), зарегистрирована в DI через `AddHostedService`. Сервис - singleton, а `AppDbContext` и репозитории - scoped, поэтому напрямую их внедрить нельзя: сервис получает `IServiceScopeFactory` и создаёт scope сам:

- раз в `PollingInterval` (10 секунд) создаёт scope, получает `AppDbContext`, выбирает из БД только идентификаторы броней в статусе `Pending` и закрывает scope;
- все найденные брони обрабатываются параллельно - `ProcessBookingAsync(Guid bookingId)` запускается для каждой брони отдельным `Task`, и итерация ждёт их завершения через `Task.WhenAll`;
- каждая задача создаёт собственный scope, а значит и собственный `DbContext`, и заново загружает бронь из БД; если бронь уже не `Pending` или удалена - она пропускается;
- для каждой брони сначала имитируется обращение к внешней системе через `Task.Delay(ProcessingDelay)` (2 секунды) - так задержки разных броней выполняются параллельно, а не суммируются;
- примитивы синхронизации не используются: у каждой задачи свой `DbContext`;
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
# -> 0 (ушли ровно 3 места, не меньше и не больше - семафор в BookingService не допустил overbooking)
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

Юнит-тесты (xUnit) находятся в проекте `EventsService.Tests`. Для тестов PostgreSQL не нужен: в них используется **InMemory-провайдер EF Core** (`UseInMemoryDatabase`). DI-контейнер настраивается через `ServiceCollection` (`TestServiceProvider`): регистрируются `AppDbContext`, реальные репозитории и сервисы. Имя InMemory-базы - `Guid.NewGuid()`, вынесенный в переменную, поэтому все scope одного теста работают с общей базой, а разные тесты друг на друга не влияют. Для параллельных запросов в тестах на конкурентность создаётся отдельный scope.

- `InMemoryEventServiceTests` - покрывает `EventService`: успешные и неуспешные сценарии CRUD, фильтрацию, пагинацию и граничные случаи;
- `InMemoryBookingServiceTests` - покрывает `BookingService`: создание брони для существующего/несуществующего/удалённого события, уникальность Id при нескольких бронях на одно событие, получение брони по Id (включая отражение смены статуса после `Confirm`/`Reject`) и получение по несуществующему Id;
- `BookingSeatManagementTests` - покрывает логику мест и защиту от овербукинга поверх реальных `EventRepository`/`BookingRepository` и `AppDbContext`:
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

ASP.NET Core(.NET 10), Entity Framework Core + PostgreSQL (Npgsql), Swashbuckle (Swagger UI), DI-контейнер встроенный в ASP.NET Core, xUnit + EF Core InMemory (в тестах).
