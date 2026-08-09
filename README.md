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

| Метод  | Путь           | Описание                 | Успех | Не найдено |
| ------ | -------------- | ------------------------ | ----- | ---------- |
| GET    | `/events`      | Список всех событий      | 200   | —          |
| GET    | `/events/{id}` | Событие по Id            | 200   | 404        |
| POST   | `/events`      | Создать событие          | 201   | —          |
| PUT    | `/events/{id}` | Обновить событие целиком | 200   | 404        |
| DELETE | `/events/{id}` | Удалить событие          | 204   | 404        |

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
- `EndAt` должен быть позже `StartAt`.
- Нарушение — `400 Bad Request` с деталями ошибок.

## Стек

ASP.NET Core(.NET 10), Swashbuckle (Swagger UI), DI-контейнер встроенный в ASP.NET Core.
