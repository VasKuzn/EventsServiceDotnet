using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventsService.Tests;

public class InMemoryBookingServiceTests : IDisposable
{
    private readonly TestServiceProvider _serviceProvider = new();

    public void Dispose() => _serviceProvider.Dispose();

    private static Event CreateEvent(int totalSeats = 100) =>
        Event.Create(Guid.NewGuid(), "Конференция .NET", "Ежегодная конференция по разработке ПО",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), totalSeats);

    private async Task SeedAsync(params object[] entities)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }

    private async Task<List<Booking>> LoadBookingsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Bookings.AsNoTracking().ToListAsync();
    }

    private async Task<T> WithServiceAsync<T>(Func<IBookingService, Task<T>> action)
    {
        using var scope = _serviceProvider.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IBookingService>());
    }

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_ReturnsPendingBooking()
    {
        // Arrange
        var existingEvent = CreateEvent();
        await SeedAsync(existingEvent);

        // Act
        var result = await WithServiceAsync(s => s.CreateBookingAsync(existingEvent.Id, CancellationToken.None));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingEvent.Id, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Null(result.ProcessedAt);

        var stored = Assert.Single(await LoadBookingsAsync());
        Assert.Equal(result.Id, stored.Id);
        Assert.Equal(BookingStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task CreateBookingAsync_CalledTwiceForSameEvent_CreatesBookingsWithUniqueIds()
    {
        // Arrange
        var existingEvent = CreateEvent();
        await SeedAsync(existingEvent);

        // Act
        var first = await WithServiceAsync(s => s.CreateBookingAsync(existingEvent.Id, CancellationToken.None));
        var second = await WithServiceAsync(s => s.CreateBookingAsync(existingEvent.Id, CancellationToken.None));

        // Assert
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(existingEvent.Id, first.EventId);
        Assert.Equal(existingEvent.Id, second.EventId);
        Assert.Equal(2, (await LoadBookingsAsync()).Count);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ExistingId_ReturnsCorrectInfo()
    {
        // Arrange
        var existingEvent = CreateEvent();
        var booking = Booking.Create(existingEvent.Id);
        await SeedAsync(existingEvent, booking);

        // Act
        var result = await WithServiceAsync(s => s.GetBookingByIdAsync(booking.Id, CancellationToken.None));

        // Assert
        Assert.Equal(booking.Id, result.Id);
        Assert.Equal(booking.EventId, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Equal(booking.CreatedAt, result.CreatedAt);
        Assert.Null(result.ProcessedAt);
    }

    [Fact]
    public async Task GetBookingByIdAsync_AfterConfirm_ReflectsConfirmedStatus()
    {
        // Arrange
        var existingEvent = CreateEvent();
        var booking = Booking.Create(existingEvent.Id);
        var processedAt = DateTime.UtcNow;
        booking.Confirm(processedAt);
        await SeedAsync(existingEvent, booking);

        // Act
        var result = await WithServiceAsync(s => s.GetBookingByIdAsync(booking.Id, CancellationToken.None));

        // Assert
        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.Equal(processedAt, result.ProcessedAt);
    }

    [Fact]
    public async Task GetBookingByIdAsync_AfterReject_ReflectsRejectedStatus()
    {
        // Arrange
        var existingEvent = CreateEvent();
        var booking = Booking.Create(existingEvent.Id);
        var processedAt = DateTime.UtcNow;
        booking.Reject(processedAt);
        await SeedAsync(existingEvent, booking);

        // Act
        var result = await WithServiceAsync(s => s.GetBookingByIdAsync(booking.Id, CancellationToken.None));

        // Assert
        Assert.Equal(BookingStatus.Rejected, result.Status);
        Assert.Equal(processedAt, result.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_NonExistingEvent_ThrowsNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => WithServiceAsync(s => s.CreateBookingAsync(Guid.NewGuid(), CancellationToken.None)));

        Assert.Empty(await LoadBookingsAsync());
    }

    [Fact]
    public async Task CreateBookingAsync_DeletedEvent_ThrowsNotFoundException()
    {
        // Arrange
        var existingEvent = CreateEvent();
        await SeedAsync(existingEvent);

        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Events.Remove(await dbContext.Events.SingleAsync(e => e.Id == existingEvent.Id));
            await dbContext.SaveChangesAsync();
        }

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => WithServiceAsync(s => s.CreateBookingAsync(existingEvent.Id, CancellationToken.None)));

        Assert.Empty(await LoadBookingsAsync());
    }

    [Fact]
    public async Task GetBookingByIdAsync_NonExistingId_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => WithServiceAsync(s => s.GetBookingByIdAsync(Guid.NewGuid(), CancellationToken.None)));
    }
}
