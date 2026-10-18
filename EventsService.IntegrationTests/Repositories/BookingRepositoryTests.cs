using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Infrastructure.DataAccess;
using EventsService.Infrastructure.Repositories;
using EventsService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EventsService.IntegrationTests.Repositories;

public class BookingRepositoryTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private BookingRepository CreateRepository(out AppDbContext context)
    {
        context = CreateContext();
        return new BookingRepository(context);
    }

    // ---------- CreateBookingAsync ----------

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_PersistsPendingBooking()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        await SeedAsync(eventEntity);
        var booking = Booking.Create(eventEntity.Id);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var created = await repository.CreateBookingAsync(booking, Ct);

        // Assert
        Assert.Equal(booking.Id, created.Id);
        await using var readContext = CreateContext();
        var stored = await readContext.Bookings.SingleAsync();
        Assert.Equal(booking.Id, stored.Id);
        Assert.Equal(eventEntity.Id, stored.EventId);
        Assert.Equal(BookingStatus.Pending, stored.Status);
        Assert.Equal(booking.CreatedAt, stored.CreatedAt, TimeSpan.FromMilliseconds(1));
        Assert.Null(stored.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_UnknownEvent_ThrowsDbUpdateExceptionAndStoresNothing()
    {
        // Arrange
        var booking = Booking.Create(Guid.NewGuid());
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var act = () => repository.CreateBookingAsync(booking, Ct);

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(act);
        await using var readContext = CreateContext();
        Assert.Empty(await readContext.Bookings.ToListAsync());
    }

    // ---------- GetBookingAsync ----------

    [Fact]
    public async Task GetBookingAsync_ExistingId_ReturnsBooking()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        var booking = Booking.Create(eventEntity.Id);
        await SeedAsync(eventEntity, booking, Booking.Create(eventEntity.Id));
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var result = await repository.GetBookingAsync(booking.Id, Ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(booking.Id, result.Id);
        Assert.Equal(eventEntity.Id, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
    }

    [Fact]
    public async Task GetBookingAsync_UnknownId_ReturnsNull()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        await SeedAsync(eventEntity, Booking.Create(eventEntity.Id));
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var result = await repository.GetBookingAsync(Guid.NewGuid(), Ct);

        // Assert
        Assert.Null(result);
    }

    // ---------- GetPendingBookingIdsAsync ----------

    [Fact]
    public async Task GetPendingBookingIdsAsync_MixedStatuses_ReturnsOnlyPendingIds()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        var pending1 = Booking.Create(eventEntity.Id);
        var pending2 = Booking.Create(eventEntity.Id);
        var confirmed = Booking.Create(eventEntity.Id);
        confirmed.Confirm(DateTime.UtcNow);
        var rejected = Booking.Create(eventEntity.Id);
        rejected.Reject(DateTime.UtcNow);
        await SeedAsync(eventEntity, pending1, pending2, confirmed, rejected);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var ids = await repository.GetPendingBookingIdsAsync(Ct);

        // Assert
        Assert.Equal(2, ids.Count);
        Assert.Contains(pending1.Id, ids);
        Assert.Contains(pending2.Id, ids);
    }

    [Fact]
    public async Task GetPendingBookingIdsAsync_NoPendingBookings_ReturnsEmptyList()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        var confirmed = Booking.Create(eventEntity.Id);
        confirmed.Confirm(DateTime.UtcNow);
        await SeedAsync(eventEntity, confirmed);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var ids = await repository.GetPendingBookingIdsAsync(Ct);

        // Assert
        Assert.Empty(ids);
    }

    [Fact]
    public async Task GetPendingBookingIdsAsync_EmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var ids = await repository.GetPendingBookingIdsAsync(Ct);

        // Assert
        Assert.Empty(ids);
    }

    // ---------- UpdateBookingAsync ----------

    [Fact]
    public async Task UpdateBookingAsync_ConfirmedBooking_PersistsStatusAndProcessedAt()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        var booking = Booking.Create(eventEntity.Id);
        var other = Booking.Create(eventEntity.Id);
        await SeedAsync(eventEntity, booking, other);
        var processedAt = new DateTime(2030, 5, 5, 12, 0, 0, DateTimeKind.Utc);

        await using var loadContext = CreateContext();
        var loaded = await new BookingRepository(loadContext).GetBookingAsync(booking.Id, Ct);
        loaded!.Confirm(processedAt);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        await repository.UpdateBookingAsync(loaded, Ct);

        // Assert
        await using var readContext = CreateContext();
        var stored = await readContext.Bookings.SingleAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Confirmed, stored.Status);
        Assert.Equal(processedAt, stored.ProcessedAt);
        var untouched = await readContext.Bookings.SingleAsync(b => b.Id == other.Id);
        Assert.Equal(BookingStatus.Pending, untouched.Status);
    }

    [Fact]
    public async Task UpdateBookingAsync_RejectedBooking_PersistsStatusAndDisappearsFromPending()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        var booking = Booking.Create(eventEntity.Id);
        await SeedAsync(eventEntity, booking);

        await using var loadContext = CreateContext();
        var loaded = await new BookingRepository(loadContext).GetBookingAsync(booking.Id, Ct);
        loaded!.Reject(DateTime.UtcNow);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        await repository.UpdateBookingAsync(loaded, Ct);

        // Assert
        await using var readContext = CreateContext();
        var stored = await readContext.Bookings.SingleAsync();
        Assert.Equal(BookingStatus.Rejected, stored.Status);
        Assert.NotNull(stored.ProcessedAt);
        Assert.Empty(await new BookingRepository(readContext).GetPendingBookingIdsAsync(Ct));
    }
}
