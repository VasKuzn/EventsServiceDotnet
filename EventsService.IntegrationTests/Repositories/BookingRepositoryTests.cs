using EventsService.Application.Services;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
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
        var user = User.Create("booking-owner", "stored-hash", UserRole.Admin);
        await SeedAsync(eventEntity, user);
        var booking = Booking.Create(eventEntity.Id, user.Id);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var created = await repository.CreateBookingAsync(booking, Ct);

        // Assert
        Assert.Equal(booking.Id, created.Id);
        await using var readContext = CreateContext();
        var stored = await readContext.Bookings.Include(b => b.User).SingleAsync();
        Assert.Equal(booking.Id, stored.Id);
        Assert.Equal(eventEntity.Id, stored.EventId);
        Assert.Equal(user.Id, stored.UserId);
        Assert.Equal(user.Login, stored.User.Login);
        Assert.Equal(user.PasswordHash, stored.User.PasswordHash);
        Assert.Equal(UserRole.Admin, stored.User.Role);
        Assert.Equal(BookingStatus.Pending, stored.Status);
        Assert.Equal(booking.CreatedAt, stored.CreatedAt, TimeSpan.FromMilliseconds(1));
        Assert.Null(stored.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_UnknownEvent_ThrowsDbUpdateExceptionAndStoresNothing()
    {
        // Arrange
        var booking = Booking.Create(Guid.NewGuid(), Booking.LegacyOwnerId);
        var repository = CreateRepository(out var context);
        await using var _ = context;

        // Act
        var act = () => repository.CreateBookingAsync(booking, Ct);

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(act);
        await using var readContext = CreateContext();
        Assert.Empty(await readContext.Bookings.ToListAsync());
    }

    [Theory]
    [InlineData(5, 20)]
    [InlineData(10, 10)]
    public async Task CreateBookingAsync_ConcurrentRequests_ReserveOnlyAvailableSeats(int seats, int requests)
    {
        var eventEntity = TestData.CreateEvent(totalSeats: seats);
        await SeedAsync(eventEntity);

        var results = await Task.WhenAll(Enumerable.Range(0, requests).Select(async _ =>
        {
            await using var context = CreateContext();
            try
            {
                var service = new BookingService(new BookingRepository(context));
                return (Guid?)(await service.CreateBookingAsync(eventEntity.Id, Booking.LegacyOwnerId, Ct)).Id;
            }
            catch (NoAvailableSeatsException)
            {
                return null;
            }
        }));

        await using var readContext = CreateContext();
        Assert.Equal(seats, results.OfType<Guid>().Distinct().Count());
        Assert.Equal(requests - seats, results.Count(id => id is null));
        Assert.Equal(seats, await readContext.Bookings.CountAsync());
        Assert.Equal(0, (await readContext.Events.SingleAsync()).AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_SeatReleasedAfterRead_RetriesWithCurrentSeatCount()
    {
        var eventEntity = TestData.CreateEvent(totalSeats: 3);
        Assert.True(eventEntity.TryReserveSeats());
        await SeedAsync(eventEntity);

        await using var bookingContext = CreateContext();
        await bookingContext.Events.SingleAsync();
        await using var releaseContext = CreateContext();
        var eventRepository = new EventRepository(releaseContext);
        var releasedEvent = (await eventRepository.GetEventAsync(eventEntity.Id, Ct))!;
        releasedEvent.ReleaseSeats();
        await eventRepository.UpdateEventAsync(releasedEvent, Ct);

        var service = new BookingService(new BookingRepository(bookingContext));
        await service.CreateBookingAsync(eventEntity.Id, Booking.LegacyOwnerId, Ct);

        await using var readContext = CreateContext();
        Assert.Equal(2, (await readContext.Events.SingleAsync()).AvailableSeats);
        Assert.Equal(1, await readContext.Bookings.CountAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_InsertFails_DoesNotReserveSeat()
    {
        var eventEntity = TestData.CreateEvent(totalSeats: 1);
        await SeedAsync(eventEntity);
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE bookings ADD CONSTRAINT reject_test_insert CHECK (false)");

        var service = new BookingService(new BookingRepository(context));
        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateBookingAsync(eventEntity.Id, Booking.LegacyOwnerId, Ct));

        await using var readContext = CreateContext();
        Assert.Equal(1, (await readContext.Events.SingleAsync()).AvailableSeats);
        Assert.Equal(0, await readContext.Bookings.CountAsync());
    }

    // ---------- GetBookingAsync ----------

    [Fact]
    public async Task GetBookingAsync_ExistingId_ReturnsBooking()
    {
        // Arrange
        var eventEntity = TestData.CreateEvent();
        var booking = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
        await SeedAsync(eventEntity, booking, Booking.Create(eventEntity.Id, Booking.LegacyOwnerId));
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
        await SeedAsync(eventEntity, Booking.Create(eventEntity.Id, Booking.LegacyOwnerId));
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
        var pending1 = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
        var pending2 = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
        var confirmed = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
        confirmed.Confirm(DateTime.UtcNow);
        var rejected = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
        rejected.Reject(DateTime.UtcNow);
        var cancelled = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
        cancelled.Cancel(DateTime.UtcNow);
        await SeedAsync(eventEntity, pending1, pending2, confirmed, rejected, cancelled);
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
        var confirmed = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
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
        var booking = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
        var other = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
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
        var booking = Booking.Create(eventEntity.Id, Booking.LegacyOwnerId);
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
