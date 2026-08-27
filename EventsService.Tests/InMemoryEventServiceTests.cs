using EventsService.Application.DataTransferObjects;
using EventsService.Application.Interfaces;
using EventsService.Application.Services;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
using Moq;

namespace EventsService.Tests;

public class InMemoryEventServiceTests
{
    private readonly Mock<IEventRepository> _eventRepositoryMock;
    private readonly InMemoryEventService _eventsService;

    public InMemoryEventServiceTests()
    {
        _eventRepositoryMock = new Mock<IEventRepository>();
        _eventsService = new InMemoryEventService(_eventRepositoryMock.Object);
    }

    [Fact]
    public async Task CreateEventAsync_ValidData_ReturnsCreatedEvent()
    {
        // Arrange
        Event? capturedEntity = null;

        var createDto = new CreateEventDto
        {
            Title = "Конференция .NET",
            Description = "Ежегодная конференция по разработке ПО",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2)
        };

        _eventRepositoryMock
            .Setup(r => r.CreateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .Callback<Event, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((Event e, CancellationToken _) => e);

        // Act
        var result = await _eventsService.CreateEventAsync(createDto, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createDto.Title, result.Title);
        Assert.Equal(createDto.Description, result.Description);
        Assert.Equal(createDto.StartAt, result.StartAt);
        Assert.Equal(createDto.EndAt, result.EndAt);
        Assert.NotEqual(Guid.Empty, result.Id);

        _eventRepositoryMock.Verify(
            r => r.CreateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(capturedEntity);
    }
    [Fact]
    public async Task GetEventAsync_ValidData_ReturnsReceivedDto()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var existingEvent = Event.Create(
            eventId,
            "Конференция .NET",
            "Ежегодная конференция по разработке ПО",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(2));

        _eventRepositoryMock
            .Setup(r => r.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        // Act
        var result = await _eventsService.GetEventAsync(eventId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingEvent.Id, result.Id);
        Assert.Equal(existingEvent.Title, result.Title);
        Assert.Equal(existingEvent.Description, result.Description);
        Assert.Equal(existingEvent.StartAt, result.StartAt);
        Assert.Equal(existingEvent.EndAt, result.EndAt);

        _eventRepositoryMock.Verify(
            r => r.GetEventAsync(eventId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventsAsync_NoFilters_ReturnsAllEvents()
    {
        // Arrange
        var events = new List<Event>
        {
            Event.Create(Guid.NewGuid(), "Конференция .NET", "Описание 1",
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            Event.Create(Guid.NewGuid(), "Митап по C#", "Описание 2",
                DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)),
            Event.Create(Guid.NewGuid(), "Воркшоп по Docker", "Описание 3",
                DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6))
        };

        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: null, from: null, to: null, page: 1, pageSize: 10,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(events.Count, result.TotalCount);
        Assert.Equal(events.Count, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(events.Select(e => e.Id), result.Items.Select(i => i.Id));

        _eventRepositoryMock.Verify(
            r => r.GetEventsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task GetEventsAsync_TitleFiltered_ReturnsAllEvents()
    {
        // Arrange
        var events = new List<Event>
        {
            Event.Create(Guid.NewGuid(), "Конференция .NET", "Описание 1",
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            Event.Create(Guid.NewGuid(), "Митап по .NET", "Описание 2",
                DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)),
            Event.Create(Guid.NewGuid(), "Воркшоп по Docker", "Описание 3",
                DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6))
        };

        var expectedIds = events
            .Where(e => e.Title.Contains(".NET", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Id);

        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: ".NET", from: null, to: null, page: 1, pageSize: 10,
            cancellationToken: CancellationToken.None);

        // Assert


        Assert.Equal(expectedIds.Count(), result.TotalCount);
        Assert.Equal(expectedIds.Count(), result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(expectedIds, result.Items.Select(i => i.Id));

        _eventRepositoryMock.Verify(
            r => r.GetEventsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task GetEventsAsync_StartAndEndDatesFiltered_ReturnsAllEvents()
    {
        // Arrange
        DateTime fromDate = DateTime.UtcNow.AddDays(1);
        DateTime toDate = DateTime.UtcNow.AddDays(4);
        var events = new List<Event>
        {
            Event.Create(Guid.NewGuid(), "Конференция .NET", "Описание 1",
                fromDate, DateTime.UtcNow.AddDays(2)),
            Event.Create(Guid.NewGuid(), "Митап по .NET", "Описание 2",
                DateTime.UtcNow.AddDays(3), toDate),
            Event.Create(Guid.NewGuid(), "Воркшоп по Docker", "Описание 3",
                DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6))
        };

        var expectedIds = events
            .Where(e => e.StartAt >= fromDate && e.EndAt <= toDate)
            .Select(e => e.Id);

        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: null, from: fromDate, to: toDate, page: 1, pageSize: 10,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(expectedIds.Count(), result.TotalCount);
        Assert.Equal(expectedIds.Count(), result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(expectedIds, result.Items.Select(i => i.Id));

        _eventRepositoryMock.Verify(
            r => r.GetEventsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task GetEventsAsync_PagitationFiltered_ReturnsAllEvents()
    {
        // Arrange
        var events = new List<Event>
        {
            Event.Create(Guid.NewGuid(), "Конференция .NET", "Описание 1",
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            Event.Create(Guid.NewGuid(), "Митап по .NET", "Описание 2",
                DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)),
            Event.Create(Guid.NewGuid(), "Воркшоп по Docker", "Описание 3",
                DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6))
        };

        var expectedIds = events.Skip(1 * 2).Take(2)
            .Select(e => e.Id);

        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: null, from: null, to: null, page: 2, pageSize: 2,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(events.Count, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(expectedIds.Count(), result.ItemsCount);
        Assert.Equal(expectedIds.Count(), result.Items.Count);
        Assert.Equal(expectedIds, result.Items.Select(i => i.Id));

        _eventRepositoryMock.Verify(
            r => r.GetEventsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task GetEventsAsync_PagitationAndDateFiltered_ReturnsAllEvents()
    {
        // Arrange
        DateTime fromDate = DateTime.UtcNow.AddDays(5);
        DateTime toDate = DateTime.UtcNow.AddDays(6);
        var events = new List<Event>
        {
            Event.Create(Guid.NewGuid(), "Конференция .NET", "Описание 1",
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            Event.Create(Guid.NewGuid(), "Митап по .NET", "Описание 2",
                DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4)),
            Event.Create(Guid.NewGuid(), "Воркшоп по Docker", "Описание 3",
                fromDate, toDate),
            Event.Create(Guid.NewGuid(), "Технический сбор по CI/CD", "Описание 4",
                DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(8))
        };

        var filteredEvents = events
            .Where(e => e.StartAt >= fromDate && e.EndAt <= toDate)
            .ToList();

        var expectedIds = filteredEvents
            .Skip(1 * 2)
            .Take(2)
            .Select(e => e.Id);

        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: null, from: fromDate, to: toDate, page: 2, pageSize: 2,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(filteredEvents.Count, result.TotalCount);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(expectedIds.Count(), result.ItemsCount);
        Assert.Equal(expectedIds.Count(), result.Items.Count);
        Assert.Equal(expectedIds, result.Items.Select(i => i.Id));

        _eventRepositoryMock.Verify(
            r => r.GetEventsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
    [Fact]
    public async Task UpdateEventAsync_ValidData_ReturnsUpdatedResult()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var updateDto = new UpdateEventDto
        {
            Title = "Обновлённая конференция .NET",
            Description = "Обновлённое описание",
            StartAt = DateTime.UtcNow.AddDays(3),
            EndAt = DateTime.UtcNow.AddDays(4)
        };

        var updatedEvent = updateDto.ToEntity(eventId);

        _eventRepositoryMock
            .Setup(r => r.UpdateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedEvent);

        // Act
        var result = await _eventsService.UpdateEventAsync(eventId, updateDto, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.Id);
        Assert.Equal(updateDto.Title, result.Title);
        Assert.Equal(updateDto.Description, result.Description);
        Assert.Equal(updateDto.StartAt, result.StartAt);
        Assert.Equal(updateDto.EndAt, result.EndAt);

        _eventRepositoryMock.Verify(
            r => r.UpdateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteEventAsync_ExistingId_ReturnsTrue()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        _eventRepositoryMock
            .Setup(r => r.DeleteEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _eventsService.DeleteEventAsync(eventId, CancellationToken.None);

        // Assert
        Assert.True(result);

        _eventRepositoryMock.Verify(
            r => r.DeleteEventAsync(eventId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        _eventRepositoryMock
            .Setup(r => r.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _eventsService.GetEventAsync(eventId, CancellationToken.None));

        _eventRepositoryMock.Verify(
            r => r.GetEventAsync(eventId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateEventAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var updateDto = new UpdateEventDto
        {
            Title = "Событие",
            Description = "Описание",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2)
        };

        _eventRepositoryMock
            .Setup(r => r.UpdateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _eventsService.UpdateEventAsync(eventId, updateDto, CancellationToken.None));

        _eventRepositoryMock.Verify(
            r => r.UpdateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteEventAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        _eventRepositoryMock
            .Setup(r => r.DeleteEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _eventsService.DeleteEventAsync(eventId, CancellationToken.None));

        _eventRepositoryMock.Verify(
            r => r.DeleteEventAsync(eventId, It.IsAny<CancellationToken>()),
            Times.Once);
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
            EndAt = DateTime.UtcNow.AddDays(2)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _eventsService.CreateEventAsync(createDto, CancellationToken.None));

        _eventRepositoryMock.Verify(
            r => r.CreateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            EndAt = startAt.AddHours(endOffsetHours)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _eventsService.CreateEventAsync(createDto, CancellationToken.None));

        _eventRepositoryMock.Verify(
            r => r.CreateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(0)]  // EndAt == StartAt
    [InlineData(-1)] // EndAt раньше StartAt
    public async Task UpdateEventAsync_EndAtNotAfterStartAt_ThrowsValidationException(int endOffsetHours)
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var startAt = DateTime.UtcNow.AddDays(1);
        var updateDto = new UpdateEventDto
        {
            Title = "Обновлённая конференция .NET",
            Description = "Описание",
            StartAt = startAt,
            EndAt = startAt.AddHours(endOffsetHours)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _eventsService.UpdateEventAsync(eventId, updateDto, CancellationToken.None));

        _eventRepositoryMock.Verify(
            r => r.UpdateEventAsync(It.IsAny<Event>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetEventsAsync_BlankTitleFilter_ReturnsAllEvents(string blankTitle)
    {
        // Arrange
        var events = new List<Event>
        {
            Event.Create(Guid.NewGuid(), "Конференция .NET", "Описание 1",
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2)),
            Event.Create(Guid.NewGuid(), "Митап по C#", "Описание 2",
                DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4))
        };

        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: blankTitle, from: null, to: null, page: 1, pageSize: 10,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(events.Count, result.TotalCount);
        Assert.Equal(events.Count, result.Items.Count);
    }

    [Fact]
    public async Task GetEventsAsync_DateFilterBoundaries_IncludesEventOnExactBoundary()
    {
        // Arrange
        var fromDate = DateTime.UtcNow.AddDays(1);
        var toDate = DateTime.UtcNow.AddDays(2);

        var boundaryEvent = Event.Create(Guid.NewGuid(), "Граничное событие", null, fromDate, toDate);
        var outsideEvent = Event.Create(Guid.NewGuid(), "Событие вне диапазона", null,
            toDate.AddSeconds(1), toDate.AddDays(1));

        var events = new List<Event> { boundaryEvent, outsideEvent };

        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: null, from: fromDate, to: toDate, page: 1, pageSize: 10,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(boundaryEvent.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task GetEventsAsync_EmptyRepository_ReturnsEmptyResult()
    {
        // Arrange
        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: null, from: null, to: null, page: 1, pageSize: 10,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.ItemsCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetEventsAsync_PageBeyondAvailableData_ReturnsEmptyItemsWithCorrectTotalCount()
    {
        // Arrange
        var events = new List<Event>
        {
            Event.Create(Guid.NewGuid(), "Единственное событие", null,
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2))
        };

        _eventRepositoryMock
            .Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(events);

        // Act
        var result = await _eventsService.GetEventsAsync(
            title: null, from: null, to: null, page: 5, pageSize: 10,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(events.Count, result.TotalCount);
        Assert.Equal(0, result.ItemsCount);
        Assert.Empty(result.Items);
    }
}
