using EventsService.Domain.Models;
using EventsService.Infrastructure.DataAccess;
using EventsService.Infrastructure.Repositories;
using EventsService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EventsService.IntegrationTests.Repositories;

public class EventRepositoryTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly DateTime Jan10 = new(2030, 1, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Jan20 = new(2030, 1, 20, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Feb01 = new(2030, 2, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Feb10 = new(2030, 2, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Mar01 = new(2030, 3, 1, 10, 0, 0, DateTimeKind.Utc);

    private EventRepository CreateRepository(out AppDbContext context)
    {
        context = CreateContext();
        return new EventRepository(context);
    }

    // ---------- CreateEventAsync ----------

    [Fact]
    public async Task CreateEventAsync_ValidEvent_PersistsEventInDatabase()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent("Conf", description: "Описание", totalSeats: 50);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var created = await repository.CreateEventAsync(eventEntity, Ct);

        // Assert
        Assert.Equal(eventEntity.Id, created.Id);
        await using var readContext = CreateContext();
        var stored = await readContext.Events.SingleAsync();
        Assert.Equal(eventEntity.Id, stored.Id);
        Assert.Equal("Conf", stored.Title);
        Assert.Equal("Описание", stored.Description);
        Assert.Equal(eventEntity.StartAt, stored.StartAt);
        Assert.Equal(eventEntity.EndAt, stored.EndAt);
        Assert.Equal(50, stored.TotalSeats);
        Assert.Equal(50, stored.AvailableSeats);
    }

    [Fact]
    public async Task CreateEventAsync_DuplicateId_ThrowsDbUpdateException()
    {
        // Arrange
        var existing = TestData.CreateEvent("First");
        await SeedAsync(existing);
        var duplicate = Event.Create(existing.Id, "Second", null, TestData.BaseDate, TestData.BaseDate.AddHours(1), 5);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var act = () => repository.CreateEventAsync(duplicate, Ct);

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(act);
    }

    // ---------- GetEventAsync ----------

    [Fact]
    public async Task GetEventAsync_ExistingId_ReturnsEvent()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent("Conf");
        await SeedAsync(eventEntity, TestData.CreateEvent("Other"));
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var result = await repository.GetEventAsync(eventEntity.Id, Ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventEntity.Id, result.Id);
        Assert.Equal("Conf", result.Title);
    }

    [Fact]
    public async Task GetEventAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        await SeedAsync(TestData.CreateEvent());
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var result = await repository.GetEventAsync(Guid.NewGuid(), Ct);

        // Assert
        Assert.Null(result);
    }

    // ---------- UpdateEventAsync ----------

    [Fact]
    public async Task UpdateEventAsync_ExistingEvent_PersistsNewValuesAndReturnsUpdatedEvent()
    {
        // Arrange
        var original = TestData.CreateEvent("Old", description: "old");
        var untouched = TestData.CreateEvent("Untouched");
        await SeedAsync(original, untouched);
        var newStart = TestData.BaseDate.AddDays(5);
        var changes = Event.Create(original.Id, "New", "new", newStart, newStart.AddHours(3), 99);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var result = await repository.UpdateEventAsync(changes, Ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New", result.Title);
        await using var readContext = CreateContext();
        var stored = await readContext.Events.SingleAsync(e => e.Id == original.Id);
        Assert.Equal("New", stored.Title);
        Assert.Equal("new", stored.Description);
        Assert.Equal(newStart, stored.StartAt);
        Assert.Equal(newStart.AddHours(3), stored.EndAt);
        Assert.Equal(99, stored.TotalSeats);
        var other = await readContext.Events.SingleAsync(e => e.Id == untouched.Id);
        Assert.Equal("Untouched", other.Title);
    }

    [Fact]
    public async Task UpdateEventAsync_UnknownId_ReturnsNullAndDoesNotCreateEvent()
    {
        // Arrange
        var missing = TestData.CreateEvent("Ghost");
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var result = await repository.UpdateEventAsync(missing, Ct);

        // Assert
        Assert.Null(result);
        await using var readContext = CreateContext();
        Assert.Empty(await readContext.Events.ToListAsync());
    }

    // ---------- DeleteEventAsync ----------

    [Fact]
    public async Task DeleteEventAsync_ExistingEvent_RemovesOnlyThatEventAndReturnsTrue()
    {
        // Arrange
        var toDelete = TestData.CreateEvent("Delete me");
        var toKeep = TestData.CreateEvent("Keep me");
        await SeedAsync(toDelete, toKeep);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var deleted = await repository.DeleteEventAsync(toDelete.Id, Ct);

        // Assert
        Assert.True(deleted);
        await using var readContext = CreateContext();
        var remaining = await readContext.Events.SingleAsync();
        Assert.Equal(toKeep.Id, remaining.Id);
    }

    [Fact]
    public async Task DeleteEventAsync_UnknownId_ReturnsFalseAndKeepsData()
    {
        // Arrange
        await SeedAsync(TestData.CreateEvent());
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var deleted = await repository.DeleteEventAsync(Guid.NewGuid(), Ct);

        // Assert
        Assert.False(deleted);
        await using var readContext = CreateContext();
        Assert.Single(await readContext.Events.ToListAsync());
    }

    [Fact]
    public async Task DeleteEventAsync_EventWithBookings_CascadeDeletesOnlyItsBookings()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        var otherEvent = TestData.CreateEvent("Other");
        await SeedAsync(
            eventEntity,
            otherEvent,
            Booking.Create(eventEntity.Id),
            Booking.Create(eventEntity.Id),
            Booking.Create(otherEvent.Id));
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        await repository.DeleteEventAsync(eventEntity.Id, Ct);

        // Assert
        await using var readContext = CreateContext();
        var bookings = await readContext.Bookings.ToListAsync();
        Assert.Single(bookings);
        Assert.Equal(otherEvent.Id, bookings[0].EventId);
    }

    // ---------- GetEventsAsync: фильтры и пагинация ----------

    private async Task SeedCatalogAsync()
    {
        await SeedAsync(
            TestData.CreateEvent("Rust workshop", Feb01),
            TestData.CreateEvent("Go day", Mar01),
            TestData.CreateEvent(".NET Conf", Jan10),
            TestData.CreateEvent("Advanced .net", Feb10),
            TestData.CreateEvent("Java meetup", Jan20));
    }

    private async Task<(IReadOnlyList<Event> Items, int TotalCount)> GetEventsAsync(
        string? title = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 10)
    {
        var repository = CreateRepository(out var context);
        await using var _ = context;
        return await repository.GetEventsAsync(title, from, to, page, pageSize, Ct);
    }

    [Fact]
    public async Task GetEventsAsync_EmptyDatabase_ReturnsEmptyResult()
    {
        // Act
        var (items, total) = await GetEventsAsync();

        // Assert
        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task GetEventsAsync_NoFilters_ReturnsAllEventsOrderedByStartAt()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync();

        // Assert
        Assert.Equal(5, total);
        Assert.Equal(
            [".NET Conf", "Java meetup", "Rust workshop", "Advanced .net", "Go day"],
            items.Select(e => e.Title));
    }

    [Theory]
    [InlineData(".NET", new[] { ".NET Conf", "Advanced .net" })]
    [InlineData(".net", new[] { ".NET Conf", "Advanced .net" })]
    [InlineData("MEETUP", new[] { "Java meetup" })]
    [InlineData("rust w", new[] { "Rust workshop" })]
    [InlineData("o", new[] { ".NET Conf", "Rust workshop", "Go day" })]
    public async Task GetEventsAsync_TitleFilter_ReturnsCaseInsensitiveSubstringMatches(string title, string[] expectedTitles)
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(title: title);

        // Assert
        Assert.Equal(expectedTitles.Length, total);
        Assert.Equal(expectedTitles, items.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_TitleWithoutMatches_ReturnsEmptyResult()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(title: "Python");

        // Assert
        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task GetEventsAsync_BlankTitle_IgnoresFilter(string? title)
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(title: title);

        // Assert
        Assert.Equal(5, total);
        Assert.Equal(5, items.Count);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    public async Task GetEventsAsync_TitleWithLikeWildcards_IsMatchedLiterally(string wildcard)
    {
        // Arrange
        await SeedAsync(
            TestData.CreateEvent("Sale 100% off", Jan10),
            TestData.CreateEvent("snake_case talk", Jan20),
            TestData.CreateEvent("Plain event", Feb01));

        // Act
        var (items, total) = await GetEventsAsync(title: wildcard);

        // Assert
        Assert.Equal(1, total);
        Assert.Single(items);
    }

    [Fact]
    public async Task GetEventsAsync_FromFilter_ReturnsEventsStartingOnOrAfterFromInclusive()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(from: Feb01);

        // Assert
        Assert.Equal(3, total);
        Assert.Equal(["Rust workshop", "Advanced .net", "Go day"], items.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_FromFilter_ExcludesEventStartingJustBeforeFrom()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, _) = await GetEventsAsync(from: Feb01.AddSeconds(1));

        // Assert
        Assert.Equal(["Advanced .net", "Go day"], items.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_ToFilter_ReturnsEventsEndingOnOrBeforeToInclusive()
    {
        // Arrange
        await SeedCatalogAsync();
        var jan20End = Jan20.AddHours(2);

        // Act
        var (items, total) = await GetEventsAsync(to: jan20End);

        // Assert
        Assert.Equal(2, total);
        Assert.Equal([".NET Conf", "Java meetup"], items.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_ToFilter_ExcludesEventEndingJustAfterTo()
    {
        // Arrange
        await SeedCatalogAsync();
        var justBeforeJan20End = Jan20.AddHours(2).AddSeconds(-1);

        // Act
        var (items, _) = await GetEventsAsync(to: justBeforeJan20End);

        // Assert
        Assert.Equal([".NET Conf"], items.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_FromAndTo_ReturnsEventsInsideRange()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(from: Jan20, to: Feb10.AddHours(2));

        // Assert
        Assert.Equal(3, total);
        Assert.Equal(["Java meetup", "Rust workshop", "Advanced .net"], items.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_FromLaterThanTo_ReturnsEmptyResult()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(from: Mar01, to: Jan10);

        // Assert
        Assert.Empty(items);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task GetEventsAsync_TitleAndDatesCombined_AppliesAllFilters()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(title: ".net", from: Feb01, to: Mar01);

        // Assert
        Assert.Equal(1, total);
        Assert.Equal(["Advanced .net"], items.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_Pagination_PagesDoNotOverlapAndCoverAllEvents()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (page1, total1) = await GetEventsAsync(page: 1, pageSize: 2);
        var (page2, total2) = await GetEventsAsync(page: 2, pageSize: 2);
        var (page3, total3) = await GetEventsAsync(page: 3, pageSize: 2);

        // Assert
        Assert.All([total1, total2, total3], total => Assert.Equal(5, total));
        Assert.Equal([".NET Conf", "Java meetup"], page1.Select(e => e.Title));
        Assert.Equal(["Rust workshop", "Advanced .net"], page2.Select(e => e.Title));
        Assert.Equal(["Go day"], page3.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_PageSizeLargerThanTotal_ReturnsAllEventsOnFirstPage()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(page: 1, pageSize: 100);

        // Assert
        Assert.Equal(5, items.Count);
        Assert.Equal(5, total);
    }

    [Fact]
    public async Task GetEventsAsync_PageBeyondData_ReturnsEmptyItemsWithTotalCount()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(page: 10, pageSize: 2);

        // Assert
        Assert.Empty(items);
        Assert.Equal(5, total);
    }

    [Fact]
    public async Task GetEventsAsync_PaginationWithFilter_TotalCountReflectsFilterNotPage()
    {
        // Arrange
        await SeedCatalogAsync();

        // Act
        var (items, total) = await GetEventsAsync(from: Jan20, page: 2, pageSize: 2);

        // Assert
        Assert.Equal(4, total);
        Assert.Equal(["Advanced .net", "Go day"], items.Select(e => e.Title));
    }

    [Fact]
    public async Task GetEventsAsync_EventsWithSameStartAt_OrderIsStableAcrossPages()
    {
        // Arrange
        await SeedAsync(Enumerable.Range(0, 6).Select(i => (object)TestData.CreateEvent($"Same {i}", Jan10)).ToArray());

        // Act
        var (page1, _) = await GetEventsAsync(page: 1, pageSize: 3);
        var (page2, _) = await GetEventsAsync(page: 2, pageSize: 3);

        // Assert
        var ids = page1.Concat(page2).Select(e => e.Id).ToList();
        Assert.Equal(6, ids.Distinct().Count());
    }
}
