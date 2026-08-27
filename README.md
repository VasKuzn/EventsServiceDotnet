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
    /* EventResponseDto[] */
  ],
  "totalCount": /* общее количество элементов прошедшее фильтрацию */,
  "page": /* количество страниц */,
  "pageSize": /* количество элементов на странице */
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

Юнит-тесты (xUnit + Moq) для `EventsService` находятся в проекте `EventsService.Tests` и покрывают `InMemoryEventService`: успешные и неуспешные сценарии CRUD, фильтрацию, пагинацию и граничные случаи.

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
