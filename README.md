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

| Поле        | Тип      | Обязательное          |
| ----------- | -------- | --------------------- |
| Id          | Guid     | генерируется сервером |
| Title       | string   | да                    |
| Description | string   | нет                   |
| StartAt     | DateTime | да                    |
| EndAt       | DateTime | да                    |

### DTO Create/UpdateEventDto

| Поле        | Тип      | Обязательное |
| ----------- | -------- | ------------ |
| Title       | string   | да           |
| Description | string   | нет          |
| StartAt     | DateTime | да           |
| EndAt       | DateTime | да           |

### Валидация (POST / PUT)

- `Title`, `StartAt`, `EndAt` обязательны.
- `EndAt` должен быть позже `StartAt` и `StartAt` > текущее время.
- Нарушение - `400 Bad Request` с деталями ошибок.

## Бронирования (Bookings)

Бронь создаётся так: `POST` мгновенно создаёт бронь в статусе `Pending` и возвращает `202 Accepted`, а фактическая обработка выполняется асинхронно фоновым сервисом.

| Метод | Путь                | Описание                      | Успех | Не найдено |
| ----- | ------------------- | ----------------------------- | ----- | ---------- |
| POST  | `/events/{id}/book` | Создать бронь для события     | 202   | 404        |
| GET   | `/bookings/{id}`    | Получить текущий статус брони | 200   | 404        |

`POST /events/{id}/book`:

- если события с `id` не существует - `404`;
- бронь создаётся сразу в статусе `Pending`;
- в теле ответа - `BookingResponseDto` созданной брони;
- в заголовке `Location` - ссылка на ресурс брони: `/bookings/{bookingId}`.

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

- раз в 10 секунд опрашивает хранилище броней и выбирает все брони в статусе `Pending`;
- для каждой найденной брони имитирует обращение к внешней системе через `Task.Delay(2s)`;
- если имитация завершилась без ошибок - бронь переводится в `Confirmed`, если во время обработки возникло исключение - в `Rejected`;
- в обоих случаях заполняется `ProcessedAt` и обновлённая бронь сохраняется в хранилище;
- корректно обрабатывает отмену (`CancellationToken`) при остановке хоста и ошибки на уровне отдельной брони/итерации, не прерывая работу сервиса;
- ход обработки логируется через `ILogger<BookingProcessingBackgroundService>`.

### Пример сценария использования

```bash
# 1. Создать событие
curl -X POST http://localhost:5000/events \
  -H "Content-Type: application/json" \
  -d '{"title":"Конференция .NET","startAt":"2026-12-01T10:00:00Z","endAt":"2026-12-01T12:00:00Z"}'
# -> 201 Created, тело содержит "id" события

# 2. Забронировать место на событии
curl -i -X POST http://localhost:5000/events/{eventId}/book
# -> 202 Accepted
# -> Location: /bookings/{bookingId}
# -> тело: {"id":"...","eventId":"...","status":0 (Pending),"createdAt":"...","processedAt":null}

# 3. Сразу проверить статус - бронь ещё не обработана
curl http://localhost:5000/bookings/{bookingId}
# -> status: 0 (Pending), processedAt: null

# 4. Подождать несколько секунд (фоновый сервис опрашивает раз в 10с + 2с имитация обработки)
sleep 12
curl http://localhost:5000/bookings/{bookingId}
# -> status: 1 (Confirmed) или 2 (Rejected), processedAt заполнен
```

## Формат ответа при ошибках

Все ошибки обрабатываются глобальным `GlobalExceptionHandler` (`EventsService.Api/Exceptions`) и возвращаются в формате **RFC ProblemDetails** (`application/problem+json`):

- `NotFoundException` (событие не найдено) - **404**
- `ValidationException` (доменная/DTO-валидация) - **400**
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
- `InMemoryBookingServiceTests` - покрывает `InMemoryBookingService`: создание брони для существующего/несуществующего/удалённого события, уникальность Id при нескольких бронях на одно событие, получение брони по Id (включая отражение смены статуса после `Confirm`/`Reject`) и получение по несуществующему Id.

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
