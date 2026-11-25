# REST API для управления событиями

Исходный код: [EventsServiceDotnet](https://github.com/VasKuzn/EventsServiceDotnet).

Для запуска требуется:

- .NET SDK 10 версии;
- **PostgreSQL** - приложение хранит события и бронирования в PostgreSQL и без доступной базы не запустится.

Проект представляет собой API для управления событиями.
Архитектура - четыре отдельных проекта (Domain - Application - Infrastructure - Presentation). Роль Presentation выполняет `EventsService.Api`:

- `EventsService.Domain` - доменные сущности `Event` и `Booking`, перечисление `BookingStatus`, доменные исключения и правила изменения состояния сущностей. Не ссылается на другие проекты или сторонние пакеты;
- `EventsService.Application` - бизнес-логика (`EventService`, `BookingService`, `BookingProcessingService`), интерфейсы сервисов и портов (`IEventRepository`, `IBookingRepository`, `IEventSeatReleaser`), DTO. Из проектов ссылается только на Domain;
- `EventsService.Infrastructure` - реализации портов: репозитории EF Core + PostgreSQL, `AppDbContext`, конфигурации сущностей, миграции и `EventSeatReleaser`. Здесь же находится фоновый `BookingProcessingBackgroundService`, который запускает обработку бронирований через Application;
- `EventsService.Api` (Presentation) - точка входа, контроллеры, HTTP-маппинг доменных исключений через `GlobalExceptionHandler`, Swagger и composition root в `Program.cs`. Стартовать приложение нужно именно отсюда.

Связи проектов заданы через `ProjectReference`:

- Application -> Domain;
- Infrastructure -> Application и Domain;
- API -> Application и Infrastructure.

Регистрация зависимостей каждого слоя вынесена в `Extensions/ServiceCollectionExtensions.cs` своего проекта:

- `AddApplicationServices()` - сервисы событий, бронирования и фоновой обработки брони;
- `AddInfrastructureServices(configuration)` - `AppDbContext`, репозитории, возврат мест и запуск фонового сервиса через `AddHostedService`.

Composition root остаётся в `EventsService.Api/Program.cs`: здесь вызываются оба extension-метода и настраивается HTTP-конвейер. Контроллеры получают запрос, вызывают сервис Application и возвращают HTTP-ответ.

Тесты находятся в отдельных проектах: `EventsService.Tests` ссылается на Application и Infrastructure, `EventsService.IntegrationTests` - на Domain и Infrastructure. Ссылок на API у тестовых проектов нет.

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

Схема БД создаётся миграциями EF Core (папка `EventsService.Infrastructure/Migrations`). При запуске `app.Services.ApplyMigrations()` вызывает `Database.Migrate()` в Infrastructure, создаёт базу при необходимости и применяет ожидающие миграции. Если база ранее была создана через `EnsureCreated()`, её нужно удалить (например, через pgAdmin), чтобы схема создавалась с нуля именно миграциями: иначе в базе останутся старые таблицы `Event` и `Booking` без истории миграций.

Начальная миграция `InitialCreate` создаёт схему:

| Таблица    | Колонки                                                                                                                                                |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `events`   | `id` (PK, uuid), `title` (varchar 250), `description` (varchar 2500, null), `start_at`, `end_at` (timestamptz), `total_seats`, `available_seats` (int) |
| `bookings` | `id` (PK, uuid), `event_id` (uuid), `status` (varchar 20), `created_at` (timestamptz), `processed_at` (timestamptz, null)                              |

Между таблицами настроен внешний ключ `bookings.event_id → events.id` с каскадным удалением (при удалении события удаляются его брони) и индекс по `bookings.event_id`.

Миграция `AddUsersAndBookingOwner` добавляет таблицу `users` с уникальным логином и обязательную связь `bookings.user_id → users.id`. Старые брони получают технического владельца без возможности входа. Миграция `AddActiveBookingIndex` добавляет индекс по владельцу и статусу для подсчёта активных броней.

Новая миграция после изменения сущностей (из корня репозитория, где лежит `EventsService.slnx`):

```powershell
dotnet ef migrations add <ИмяМиграции> --project EventsService.Infrastructure --startup-project EventsService.Api --output-dir Migrations
```

Для команд `dotnet ef` нужен инструмент `dotnet-ef` (`dotnet tool install --global dotnet-ef`) версии, совпадающей с версией EF Core в проекте.

### Параметры JWT

API читает секцию `Jwt` из `EventsService.Api/appsettings.json`. В ней заданы издатель (`Issuer`), аудитория (`Audience`) и срок действия (`LifetimeMinutes`). Для запуска передайте ключ подписи длиной от 32 байт через переменную окружения `Jwt__Secret`.

Сгенерируйте секрет в PowerShell и запустите API из того же терминала.

```powershell
$jwtKey = New-Object byte[] 32
$jwtRng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtRng.GetBytes($jwtKey)
$jwtRng.Dispose()
$env:Jwt__Secret = [Convert]::ToBase64String($jwtKey)
```

## Запуск (в папке EventsService)

Команды выполняются из корня репозитория, где лежит `EventsService.slnx`:

```bash
dotnet restore
dotnet build
dotnet run --project EventsService.Api
```

После запуска Swagger UI доступен по адресу, указанному в консоли - `http://localhost:5000/swagger`.

## Аутентификация

- `POST /auth/register` принимает `login`, `password` и необязательное поле `role`. По умолчанию используется `User`, для тестирования можно указать `Admin`. Успешная регистрация возвращает `204`, ошибка валидации или занятый логин — `400`.
- `POST /auth/login` принимает `login` и `password`. Успешный вход возвращает `200` и объект `{ "token": "..." }`, неверные учётные данные — `400`.
- Для защищённых запросов передавайте `Authorization: Bearer <token>` или введите токен через **Authorize** в Swagger. Без действующего токена API возвращает `401`.

## API

Базовый путь: `/events`

| Метод  | Путь           | Описание                                  | Успех | Не найдено |
| ------ | -------------- | ----------------------------------------- | ----- | ---------- |
| GET    | `/events`      | Список событий (с фильтрами и пагинацией) | 200   | —          |
| GET    | `/events/{id}` | Событие по Id                             | 200   | 404        |
| POST   | `/events`      | Создать событие                           | 201   | —          |
| PUT    | `/events/{id}` | Обновить событие целиком                  | 200   | 404        |
| DELETE | `/events/{id}` | Удалить событие                           | 204   | 404        |

Чтение событий доступно без токена. Создание, редактирование и удаление требуют роли `Admin`. Запрос с токеном обычного пользователя возвращает `403`.

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

| Метод  | Путь                | Описание                        | Успех |
| ------ | ------------------- | ------------------------------- | ----- |
| POST   | `/events/{id}/book` | Создать бронь для события       | 202   |
| GET    | `/bookings/{id}`    | Получить данные брони по ID     | 200   |
| DELETE | `/bookings/{id}`    | Отменить бронь и вернуть место  | 204   |

Все три маршрута требуют JWT. Отменить бронь может её владелец или администратор. Чужая отмена от обычного пользователя возвращает `403`, отсутствующая бронь — `404`. Повторная отмена возвращает `204`, место освобождается один раз.

`POST /events/{id}/book`:

- если события с `id` не существует - `404`;
- если событие уже началось — `400`;
- если у события не осталось свободных мест (`AvailableSeats == 0`) - `409 Conflict` (`NoAvailableSeatsException`);
- если у пользователя уже 10 броней в статусах `Pending` или `Confirmed` — `409`;
- иначе место резервируется и бронь создаётся сразу в статусе `Pending`;
- в теле ответа - `BookingResponseDto` созданной брони;
- в заголовке `Location` - ссылка на ресурс брони: `/bookings/{bookingId}`.

### Защита от овербукинга

Репозиторий сохраняет списание места и создание брони одним `SaveChangesAsync`. Системный столбец PostgreSQL `xmin` служит токеном конкурентности события. Если другой запрос успел изменить событие, репозиторий заново читает число мест и повторяет попытку. Тот же токен защищает возврат мест в `EventSeatReleaser`, в том числе при работе нескольких экземпляров приложения.

Проверка лимита и создание брони выполняются в транзакции с блокировкой строки пользователя. При отмене изменение статуса и возврат места сохраняются атомарно. Проверка предыдущего статуса при записи защищает отменённую бронь от фонового подтверждения.

### Модель Booking / BookingResponseDto

| Поле        | Тип           | Обязательное                                |
| ----------- | ------------- | ------------------------------------------- |
| Id          | Guid          | генерируется сервером                       |
| EventId     | Guid          | да - Id события, к которому относится бронь |
| UserId      | Guid          | да - Id владельца из JWT                    |
| Status      | BookingStatus | да, по умолчанию `Pending`                  |
| CreatedAt   | DateTime      | да, проставляется при создании              |
| ProcessedAt | DateTime?     | заполняется после фоновой обработки         |

### Статусы (`BookingStatus`)

| Статус      | Значение | Описание                                         |
| ----------- | -------- | ------------------------------------------------ |
| `Pending`   | 0        | бронь создана, ожидает фоновой обработки         |
| `Confirmed` | 1        | бронь подтверждена (обработка прошла без ошибок) |
| `Rejected`  | 2        | бронь отклонена (при обработке возникла ошибка)  |
| `Cancelled` | 3        | бронь отменена                                   |

Данные о бронированиях и событиях хранятся в PostgreSQL (таблицы `bookings` и `events`) и сохраняются между перезапусками приложения. Вся работа с `AppDbContext` инкапсулирована в репозиториях `EventRepository` и `BookingRepository` (`EventsService.Infrastructure/Repositories`, интерфейсы `IEventRepository` и `IBookingRepository` - в Application), сервисы обращаются к данным только через них. Репозитории зарегистрированы как `Scoped`. Фильтрация и пагинация списка событий выполняются на стороне БД в `EventRepository.GetEventsAsync` (сортировка по `StartAt`, затем по `Id`, чтобы страницы не пересекались).

### Фоновая обработка (`BookingProcessingBackgroundService`)

Реализована как `BackgroundService` (`EventsService.Infrastructure/BackgroundServices`), зарегистрирована в DI через `AddInfrastructureServices` → `AddHostedService`. Сервис - singleton, а `IBookingProcessingService`, `AppDbContext` и репозитории - scoped, поэтому сервис получает `IServiceScopeFactory` и создаёт scope сам:

- раз в `PollingInterval` (10 секунд между итерациями) создаёт scope, получает `IBookingProcessingService` и запрашивает идентификаторы `Pending`-броней через `GetPendingBookingIdsAsync`;
- все найденные брони обрабатываются параллельно; для каждой сначала выполняется `Task.Delay(ProcessingDelay)` (2 секунды), затем создаётся отдельный scope и вызывается `IBookingProcessingService.ProcessBookingAsync`;
- завершение задач ожидается через `Task.WhenAll`; отмена и ошибки итерации обрабатываются фоновым сервисом.

Решения о статусе брони находятся в `BookingProcessingService` (`EventsService.Application/Services`):

- заново загружает бронь через `IBookingRepository`; если она удалена или уже не `Pending` - пропускает её;
- если событие не найдено - переводит бронь в `Rejected`; при обычном удалении события сама бронь удаляется каскадно;
- если событие найдено - переводит бронь в `Confirmed`;
- если ошибка возникла до смены статуса - отклоняет бронь, сохраняет её и вызывает порт `IEventSeatReleaser`, чтобы вернуть занятое место;
- в обоих терминальных статусах заполняется `ProcessedAt`;
- отмену передаёт вызывающему коду; если сохранение уже изменённого статуса завершилось ошибкой, передаёт исходную ошибку без повторного перехода в `Rejected`.

Возврат мест реализован в `EventSeatReleaser` (`EventsService.Infrastructure/Services`): загружает событие, вызывает доменный метод `Event.ReleaseSeats()` и сохраняет результат через репозиторий. При `DbUpdateConcurrencyException` делает до трёх попыток, каждый раз создавая новый scope для загрузки актуального состояния. Application знает только об интерфейсе `IEventSeatReleaser` и не зависит от EF Core.

Ход обработки брони логируется через `ILogger<BookingProcessingService>`, ошибки фонового цикла - через `ILogger<BookingProcessingBackgroundService>`, повторы возврата мест - через `ILogger<EventSeatReleaser>`.

### Пример сценария использования

```bash
# Зарегистрировать администратора и пользователя
curl -fsS http://localhost:5000/auth/register --json '{"login":"demo-admin","password":"demo-admin-password","role":"Admin"}'
curl -fsS http://localhost:5000/auth/register --json '{"login":"demo-user","password":"demo-user-password"}'
ADMIN_TOKEN=$(curl -fsS http://localhost:5000/auth/login --json '{"login":"demo-admin","password":"demo-admin-password"}' | jq -r '.token')
USER_TOKEN=$(curl -fsS http://localhost:5000/auth/login --json '{"login":"demo-user","password":"demo-user-password"}' | jq -r '.token')

# 1. Создать событие на 1 место
curl -X POST http://localhost:5000/events \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"Конференция .NET","startAt":"2030-12-01T10:00:00Z","endAt":"2030-12-01T12:00:00Z","totalSeats":1}'
# -> 201 Created, тело содержит "id" события, "totalSeats":1, "availableSeats":1

# 2. Забронировать место на событии
curl -i -X POST http://localhost:5000/events/{eventId}/book -H "Authorization: Bearer $USER_TOKEN"
# -> 202 Accepted
# -> Location: /bookings/{bookingId}
# -> тело содержит id, eventId, userId и status: 0 (Pending)

# 3. Сразу проверить статус - бронь ещё не обработана
curl http://localhost:5000/bookings/{bookingId} -H "Authorization: Bearer $USER_TOKEN"
# -> status: 0 (Pending), processedAt: null

# 4. Повторная попытка забронировать то же событие - мест больше нет
curl -i -X POST http://localhost:5000/events/{eventId}/book -H "Authorization: Bearer $USER_TOKEN"
# -> 409 Conflict

# 5. Подождать несколько секунд (фоновый сервис опрашивает раз в PollingInterval=10с + ProcessingDelay=2с имитация обработки)
sleep 12
curl http://localhost:5000/bookings/{bookingId} -H "Authorization: Bearer $USER_TOKEN"
# -> status: 1 (Confirmed) или 2 (Rejected), processedAt заполнен
# если обработка отклонена из-за ошибки до смены статуса и событие существует - место возвращается
# при удалении события его брони удаляются каскадно

# 6. Отменить активную бронь и освободить место
curl -i -X DELETE http://localhost:5000/bookings/{bookingId} -H "Authorization: Bearer $USER_TOKEN"
# -> 204 No Content
```

### Пример сценария с овербукингом

Выполните пример с переменными `ADMIN_TOKEN` и `USER_TOKEN` из предыдущего сценария. При параллельных запросах число успешных броней ограничено количеством мест.

```bash
# 1. Создать событие на 3 места
eventId=$(curl -s -X POST http://localhost:5000/events \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"title":"Конференция .NET","startAt":"2030-12-01T10:00:00Z","endAt":"2030-12-01T12:00:00Z","totalSeats":3}' \
  | jq -r '.id')

# 2. Отправить 10 конкурентных запросов на бронирование (параллельно, в фоне)
for i in $(seq 1 10); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "http://localhost:5000/events/${eventId}/book" -H "Authorization: Bearer $USER_TOKEN" &
done
wait
# -> в выводе ровно 3 строки "202" и 7 строк "409" (порядок непредсказуем из-за конкуренции)

# 3. Проверить итоговое количество мест
curl -s http://localhost:5000/events/${eventId} | jq '.availableSeats'
# -> 0 (зарезервировано ровно 3 места)
```

## Формат ответа при ошибках

Все ошибки обрабатываются глобальным `GlobalExceptionHandler` (`EventsService.Api/Exceptions`) и возвращаются в формате **RFC ProblemDetails** (`application/problem+json`):

- `NotFoundException` (событие/бронь не найдены) - **404**
- `ValidationException` (доменная/DTO-валидация, включая `TotalSeats <= 0`) - **400**
- `EventAlreadyStartedException` (событие уже началось) — **400**
- `ForbiddenException` (пользователь пытается отменить чужую бронь) — **403**
- `NoAvailableSeatsException` (нет свободных мест), `ActiveBookingLimitExceededException` (лимит активных броней) и `BookingStateException` (недопустимый переход статуса) — **409**
- Необработанное исключение - **500**

**404 Not Found:**

```json
{
  "type": "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Сущность с Id: {id} не найдена"
}
```

**400 Bad Request** (если у `ValidationException` указано конкретное поле, добавляется `errors`):

```json
{
  "type": "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
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
  "type": "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "No available seats for this event"
}
```

**500 Internal Server Error:**

```json
{
  "type": "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An internal server error has occurred. Please try again later."
}
```

В окружении `Development` в `detail` для 500 попадает исходное сообщение исключения, а не общий текст.

## Тесты

Юнит-тесты (xUnit) находятся в проекте `EventsService.Tests`. `TestServiceProvider` регистрирует `AppDbContext` с **InMemory-провайдером EF Core** и подключает общие регистрации адаптеров и сервисов. У каждого теста своя InMemory-база.

- `InMemoryEventServiceTests` - покрывает `EventService`: успешные и неуспешные сценарии CRUD, фильтрацию, пагинацию и граничные случаи;
- `InMemoryBookingServiceTests` - покрывает `BookingService`: создание брони для существующего/несуществующего/удалённого события, уникальность Id при нескольких бронях на одно событие, получение брони по Id (включая отражение смены статуса после `Confirm`/`Reject`) и получение по несуществующему Id;
- `BookingSeatManagementTests` - покрывает логику мест поверх реальных `EventRepository`/`BookingRepository` и `AppDbContext`:
  - уменьшение `AvailableSeats` после успешной брони, создание броней до исчерпания лимита с уникальными Id, `NoAvailableSeatsException` после исчерпания мест, `NotFoundException` для несуществующего события;
  - смена статуса брони через `Confirm()`/`Reject()` (статус + `ProcessedAt`), запрет повторной обработки, восстановление `AvailableSeats` и возможность новой брони после `Reject()` + `ReleaseSeats()`.

- `BookingProcessingServiceTests` - подтверждение брони, пропуск отсутствующей/обработанной брони, отклонение при отсутствии события, возврат места при ошибке обработки, отмена и ошибка сохранения статуса. Зависимости заменяются через Moq;
- `EventSeatReleaserTests` - возврат места, повторная загрузка события после конфликта EF Core и ограничение числа попыток. Репозитории заменяются через Moq, реальные БД и Docker не нужны.

Запуск юнит-тестов (из корня репозитория):

```bash
dotnet test EventsService.Tests/EventsService.Tests.csproj
```

Либо запустить тесты для всего solution (нужен запущенный Docker для интеграционных тестов):

```bash
dotnet test
```

### Интеграционные тесты (Testcontainers)

Интеграционные тесты находятся в проекте `EventsService.IntegrationTests` и работают с **реальным PostgreSQL**, который поднимается в Docker через [Testcontainers](https://dotnet.testcontainers.org/) (`Testcontainers.PostgreSql`, образ `postgres:16-alpine`). Для запуска нужен **запущенный Docker**; PostgreSQL устанавливать не требуется, контейнер стартует и удаляется автоматически:

```bash
dotnet test EventsService.IntegrationTests/EventsService.IntegrationTests.csproj
```

Как устроены тесты:

- `PostgresFixture` (`IAsyncLifetime`) поднимает **один контейнер на все тесты** и отдаёт строку подключения из объекта контейнера (порт назначается динамически, ничего не захардкожено);
- перед **каждым** тестом база приводится к чистому состоянию: `EnsureDeleted()` + `Migrate()` (`DatabaseTestBase`), поэтому схема всегда создаётся именно миграциями, а тесты не зависят друг от друга и от порядка запуска;
- все тестовые классы входят в одну xUnit-коллекцию `Postgres` (общий контейнер, последовательное выполнение);
- тесты оформлены по схеме Arrange / Act / Assert; проверки читают данные через новый `DbContext`, а не из change tracker.

Что покрыто:

- `MigrationTests` - применение миграций, перенос старых броней на технического владельца, повторный `Migrate()`, соответствие миграций модели, структура таблиц, индексы и внешние ключи;
- `EventRepositoryTests` - все методы `EventRepository`: создание (в том числе дубликат Id), получение по Id, обновление существующего/несуществующего события, удаление (в том числе каскадное удаление броней), список событий: все варианты фильтров (`title` - регистр, подстрока, пустое значение, спецсимволы `%` и `_`; `from`/`to` - включительные границы; комбинации фильтров; пустой результат) и пагинация (страницы не пересекаются, последняя неполная страница, страница за пределами данных, `totalCount` с учётом фильтра, стабильный порядок);
- `BookingRepositoryTests` - создание и получение брони, выборка `Pending`-броней, обновление статуса, параллельное бронирование до исчерпания мест, повтор при конкурентном возврате места и откат списания при ошибке вставки.
- `BookingApiTests` — регистрация и вход, права на управление событиями, отмена владельцем и администратором, Swagger и лимит при параллельном бронировании разных событий.

## Стек

ASP.NET Core(.NET 10), Entity Framework Core + PostgreSQL (Npgsql), Swashbuckle (Swagger UI), DI-контейнер встроенный в ASP.NET Core, xUnit + EF Core InMemory (юнит-тесты), xUnit + Testcontainers.PostgreSql (интеграционные тесты).
