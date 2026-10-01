using EventsService.Application.DataTransferObjects.Events;
using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventsService.Tests;

public class InMemoryEventServiceTests : IDisposable
{
    private readonly TestServiceProvider _serviceProvider = new();

    public void Dispose() => _serviceProvider.Dispose();

    private async Task SeedAsync(params Event[] events)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Events.AddRange(events);
        await dbContext.SaveChangesAsync();
    }

    private async Task<List<Event>> LoadEventsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Events.AsNoTracking().ToListAsync();
    }

    private async Task<T> WithServiceAsync<T>(Func<IEventService, Task<T>> action)
    {
        using var scope = _serviceProvider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IEventService>());
    }

    private Task<PaginatedResult<EventResponseDto>> GetEventsAsync(
        string? title = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 10) =>
        WithServiceAsync(s => s.GetEventsAsync(title, from, to, page, pageSize, CancellationToken.None));

    private static Event NewEvent(string title, DateTime startAt, DateTime endAt, string? description = "Описание") =>
        Event.Create(Guid.NewGuid(), title, description, startAt, endAt, 100);

    [Fact]
    public async Task CreateEventAsync_ValidData_ReturnsCreatedEvent()
    {
        // Arrange
        var createDto = new CreateEventDto
        {
            Title = "Конференция .NET",
            Description = "Ежегодная конференция по разработке ПО",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100
        };

        // Act
        var result = await WithServiceAsync(s => s.CreateEventAsync(createDto, CancellationToken.None));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createDto.Title, result.Title);
        Assert.Equal(createDto.Description, result.Description);
        Assert.Equal(createDto.StartAt, result.StartAt);
        Assert.Equal(createDto.EndAt, result.EndAt);
        Assert.NotEqual(Guid.Empty, result.Id);

        var stored = Assert.Single(await LoadEventsAsync());
        Assert.Equal(result.Id, stored.Id);
        Assert.Equal(100, stored.AvailableSeats);
    }

    [Fact]
    public async Task GetEventAsync_ValidData_ReturnsReceivedDto()
    {
        // Arrange
        var existingEvent = NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2),
            "Ежегодная конференция по разработке ПО");
        await SeedAsync(existingEvent);

        // Act
        var result = await WithServiceAsync(s => s.GetEventAsync(existingEvent.Id, CancellationToken.None));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingEvent.Id, result.Id);
        Assert.Equal(existingEvent.Title, result.Title);
        Assert.Equal(existingEvent.Description, result.Description);
        Assert.Equal(existingEvent.StartAt, result.StartAt);
        Assert.Equal(existingEvent.EndAt, result.EndAt);
    }

    [Fact]
    public async Task GetEventsAsync_NoFilters_ReturnsAllEvents()
    {
        // Arrange
        var events = new[]
        {
            NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            NewEvent("Митап по C#", DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)),
            NewEvent("Воркшоп по Docker", DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6))
        };
        await SeedAsync(events);

        // Act
        var result = await GetEventsAsync();

        // Assert
        Assert.Equal(events.Length, result.TotalCount);
        Assert.Equal(events.Length, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(
            events.Select(e => e.Id).OrderBy(id => id),
            result.Items.Select(i => i.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetEventsAsync_TitleFiltered_ReturnsMatchingEvents()
    {
        // Arrange
        var events = new[]
        {
            NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            NewEvent("Митап по .NET", DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)),
            NewEvent("Воркшоп по Docker", DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6))
        };
        await SeedAsync(events);

        var expectedIds = events
            .Where(e => e.Title.Contains(".NET", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id)
            .OrderBy(id => id)
            .ToList();

        // Act
        var result = await GetEventsAsync(title: ".NET");

        // Assert
        Assert.Equal(expectedIds.Count, result.TotalCount);
        Assert.Equal(expectedIds.Count, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(expectedIds, result.Items.Select(i => i.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetEventsAsync_StartAndEndDatesFiltered_ReturnsEventsInsideRange()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(1);
        var toDate = DateTime.UtcNow.AddDays(4);
        var events = new[]
        {
            NewEvent("Конференция .NET", fromDate, DateTime.UtcNow.AddDays(2)),
            NewEvent("Митап по .NET", DateTime.UtcNow.AddDays(3), toDate),
            NewEvent("Воркшоп по Docker", DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6))
        };
        await SeedAsync(events);

        var expectedIds = events
            .Where(e => e.StartAt >= fromDate && e.EndAt <= toDate)
            .Select(e => e.Id)
            .OrderBy(id => id)
            .ToList();

        // Act
        var result = await GetEventsAsync(from: fromDate, to: toDate);

        // Assert
        Assert.Equal(expectedIds.Count, result.TotalCount);
        Assert.Equal(expectedIds.Count, result.Items.Count);
        Assert.Equal(expectedIds, result.Items.Select(i => i.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task GetEventsAsync_Pagination_PagesDoNotOverlapAndCoverAllEvents()
    {
        // Arrange
        var events = new[]
        {
            NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            NewEvent("Митап по .NET", DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)),
            NewEvent("Воркшоп по Docker", DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6))
        };
        await SeedAsync(events);

        // Act
        var firstPage = await GetEventsAsync(page: 1, pageSize: 2);
        var secondPage = await GetEventsAsync(page: 2, pageSize: 2);

        // Assert
        Assert.Equal(events.Length, secondPage.TotalCount);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(2, secondPage.PageSize);
        Assert.Equal(2, firstPage.ItemsCount);
        Assert.Equal(1, secondPage.ItemsCount);
        Assert.Single(secondPage.Items);

        var allIds = firstPage.Items.Concat(secondPage.Items).Select(i => i.Id).ToList();
        Assert.Equal(events.Length, allIds.Distinct().Count());
        Assert.Equal(events.Select(e => e.Id).OrderBy(id => id), allIds.OrderBy(id => id));
    }

    [Fact]
    public async Task GetEventsAsync_PaginationAndDateFiltered_ReturnsEmptyPageWithFilteredTotalCount()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(5);
        var toDate = DateTime.UtcNow.AddDays(6);
        await SeedAsync(
            NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            NewEvent("Митап по .NET", DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)),
            NewEvent("Воркшоп по Docker", fromDate, toDate),
            NewEvent("Технический сбор по CI/CD", DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(8)));

        // Act
        var result = await GetEventsAsync(from: fromDate, to: toDate, page: 2, pageSize: 2);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(0, result.ItemsCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task UpdateEventAsync_ValidData_ReturnsUpdatedResult()
    {
        // Arrange
        var existingEvent = NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));
        await SeedAsync(existingEvent);

        var updateDto = new UpdateEventDto
        {
            Title = "Обновлённая конференция .NET",
            Description = "Обновлённое описание",
            StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(4),
            TotalSeats = 100
        };

        // Act
        var result = await WithServiceAsync(
            s => s.UpdateEventAsync(existingEvent.Id, updateDto, CancellationToken.None));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingEvent.Id, result.Id);
        Assert.Equal(updateDto.Title, result.Title);
        Assert.Equal(updateDto.Description, result.Description);
        Assert.Equal(updateDto.StartAt, result.StartAt);
        Assert.Equal(updateDto.EndAt, result.EndAt);

        var stored = Assert.Single(await LoadEventsAsync());
        Assert.Equal(updateDto.Title, stored.Title);
        Assert.Equal(updateDto.Description, stored.Description);
    }

    [Fact]
    public async Task DeleteEventAsync_ExistingId_ReturnsTrue()
    {
        // Arrange
        var existingEvent = NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));
        await SeedAsync(existingEvent);

        // Act
        var result = await WithServiceAsync(s => s.DeleteEventAsync(existingEvent.Id, CancellationToken.None));

        // Assert
        Assert.True(result);
        Assert.Empty(await LoadEventsAsync());
    }

    [Fact]
    public async Task GetEventAsync_NonExistingId_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => WithServiceAsync(s => s.GetEventAsync(Guid.NewGuid(), CancellationToken.None)));
    }

    [Fact]
    public async Task UpdateEventAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        var updateDto = new UpdateEventDto
        {
            Title = "Событие",
            Description = "Описание",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => WithServiceAsync(s => s.UpdateEventAsync(Guid.NewGuid(), updateDto, CancellationToken.None)));

        Assert.Empty(await LoadEventsAsync());
    }

    [Fact]
    public async Task DeleteEventAsync_NonExistingId_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => WithServiceAsync(s => s.DeleteEventAsync(Guid.NewGuid(), CancellationToken.None)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateEventAsync_BlankTitle_ThrowsValidationException(string blankTitle)
    {
        // Arrange
        var createDto = new CreateEventDto
        {
            Title = blankTitle,
            Description = "Описание",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 100
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => WithServiceAsync(s => s.CreateEventAsync(createDto, CancellationToken.None)));

        Assert.Empty(await LoadEventsAsync());
    }

    [Theory]
    [InlineData(0)]  // EndAt == StartAt — граничное значение, тоже невалидно
    [InlineData(-1)] // EndAt раньше StartAt
    public async Task CreateEventAsync_EndAtNotAfterStartAt_ThrowsValidationException(int endOffsetHours)
    {
        // Arrange
        var startAt = DateTime.UtcNow.AddDays(1);
        var createDto = new CreateEventDto
        {
            Title = "Конференция .NET",
            Description = "Описание",
            StartAt = startAt,
            EndAt = startAt.AddHours(endOffsetHours),
            TotalSeats = 100
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => WithServiceAsync(s => s.CreateEventAsync(createDto, CancellationToken.None)));

        Assert.Empty(await LoadEventsAsync());
    }

    [Theory]
    [InlineData(0)]  // EndAt == StartAt
    [InlineData(-1)] // EndAt раньше StartAt
    public async Task UpdateEventAsync_EndAtNotAfterStartAt_ThrowsValidationException(int endOffsetHours)
    {
        // Arrange
        var existingEvent = NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));
        await SeedAsync(existingEvent);

        var startAt = DateTime.UtcNow.AddDays(1);
        var updateDto = new UpdateEventDto
        {
            Title = "Обновлённая конференция .NET",
            Description = "Описание",
            StartAt = startAt,
            EndAt = startAt.AddHours(endOffsetHours),
            TotalSeats = 100
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => WithServiceAsync(s => s.UpdateEventAsync(existingEvent.Id, updateDto, CancellationToken.None)));

        var stored = Assert.Single(await LoadEventsAsync());
        Assert.Equal(existingEvent.Title, stored.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetEventsAsync_BlankTitleFilter_ReturnsAllEvents(string blankTitle)
    {
        // Arrange
        await SeedAsync(
            NewEvent("Конференция .NET", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            NewEvent("Митап по C#", DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)));

        // Act
        var result = await GetEventsAsync(title: blankTitle);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetEventsAsync_DateFilterBoundaries_IncludesEventOnExactBoundary()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(1);
        var toDate = DateTime.UtcNow.AddDays(2);

        var boundaryEvent = NewEvent("Граничное событие", fromDate, toDate, null);
        var outsideEvent = NewEvent("Событие вне диапазона", toDate.AddSeconds(1), toDate.AddDays(1), null);
        await SeedAsync(boundaryEvent, outsideEvent);

        // Act
        var result = await GetEventsAsync(from: fromDate, to: toDate);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(boundaryEvent.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task GetEventsAsync_EmptyDatabase_ReturnsEmptyResult()
    {
        var result = await GetEventsAsync();

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.ItemsCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetEventsAsync_PageBeyondAvailableData_ReturnsEmptyItemsWithCorrectTotalCount()
    {
        // Arrange
        await SeedAsync(NewEvent("Единственное событие", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), null));

        // Act
        var result = await GetEventsAsync(page: 5, pageSize: 10);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(0, result.ItemsCount);
        Assert.Empty(result.Items);
    }
}
