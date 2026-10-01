using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventsService.Tests;

public class BookingSeatManagementTests : IDisposable
{
    private readonly TestServiceProvider _serviceProvider = new();

    public void Dispose() => _serviceProvider.Dispose();

    private static Event CreateTestEvent(int totalSeats = 10) =>
        Event.Create(Guid.NewGuid(), "Конференция .NET", "Ежегодная конференция по разработке ПО",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), totalSeats);

    private async Task<Event> SeedEventAsync(int totalSeats)
    {
        var testEvent = CreateTestEvent(totalSeats);

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Events.Add(testEvent);
        await dbContext.SaveChangesAsync();

        return testEvent;
    }

    private async Task<int> GetAvailableSeatsAsync(Guid eventId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await dbContext.Events.AsNoTracking().SingleAsync(e => e.Id == eventId);
        return stored.AvailableSeats;
    }

    private async Task<BookingResponseDto> BookAsync(Guid eventId)
    {
        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        return await bookingService.CreateBookingAsync(eventId, CancellationToken.None);
    }

    // Отклоняет бронь и возвращает место - то же самое делает фоновый сервис при ошибке обработки.
    private async Task RejectBookingAndReleaseSeatAsync(Guid bookingId, Guid eventId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var booking = await dbContext.Bookings.SingleAsync(b => b.Id == bookingId);
        var testEvent = await dbContext.Events.SingleAsync(e => e.Id == eventId);

        booking.Reject(DateTime.UtcNow);
        testEvent.ReleaseSeats();

        await dbContext.SaveChangesAsync();
    }

    // ---------- Успешные сценарии ----------

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_DecreasesAvailableSeatsByOne()
    {
        var testEvent = await SeedEventAsync(totalSeats: 10);

        await BookAsync(testEvent.Id);

        Assert.Equal(9, await GetAvailableSeatsAsync(testEvent.Id));
    }

    [Fact]
    public async Task CreateBookingAsync_UpToCapacity_AllSucceedWithUniqueIds()
    {
        var testEvent = await SeedEventAsync(totalSeats: 5);

        var results = new List<BookingResponseDto>();
        for (var i = 0; i < 5; i++)
        {
            results.Add(await BookAsync(testEvent.Id));
        }

        Assert.Equal(5, results.Select(r => r.Id).Distinct().Count());
        Assert.All(results, r => Assert.Equal(BookingStatus.Pending, r.Status));
        Assert.Equal(0, await GetAvailableSeatsAsync(testEvent.Id));
    }

    [Fact]
    public async Task CreateBookingAsync_SeatsExhausted_NextAttemptThrowsNoAvailableSeatsException()
    {
        var testEvent = await SeedEventAsync(totalSeats: 1);

        await BookAsync(testEvent.Id);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => BookAsync(testEvent.Id));
    }

    // ---------- Неуспешные сценарии ----------

    [Fact]
    public async Task CreateBookingAsync_NonExistingEvent_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => BookAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateBookingAsync_NoAvailableSeats_ThrowsNoAvailableSeatsException()
    {
        var testEvent = await SeedEventAsync(totalSeats: 1);

        await BookAsync(testEvent.Id);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => BookAsync(testEvent.Id));

        Assert.Equal(0, await GetAvailableSeatsAsync(testEvent.Id));
    }

    // ---------- Смена статуса брони ----------

    [Fact]
    public void Confirm_PendingBooking_SetsConfirmedStatusAndProcessedAt()
    {
        var booking = Booking.Create(Guid.NewGuid());
        var processedAt = DateTime.UtcNow;

        booking.Confirm(processedAt);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.Equal(processedAt, booking.ProcessedAt);
    }

    [Fact]
    public void Reject_PendingBooking_SetsRejectedStatusAndProcessedAt()
    {
        var booking = Booking.Create(Guid.NewGuid());
        var processedAt = DateTime.UtcNow;

        booking.Reject(processedAt);

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.Equal(processedAt, booking.ProcessedAt);
    }

    [Fact]
    public async Task RejectBooking_ReleaseSeats_RestoresAvailableSeatsCount()
    {
        var testEvent = await SeedEventAsync(totalSeats: 3);

        var bookingDto = await BookAsync(testEvent.Id);
        Assert.Equal(2, await GetAvailableSeatsAsync(testEvent.Id));

        await RejectBookingAndReleaseSeatAsync(bookingDto.Id, testEvent.Id);

        Assert.Equal(3, await GetAvailableSeatsAsync(testEvent.Id));

        using var scope = _serviceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var stored = await bookingService.GetBookingByIdAsync(bookingDto.Id, CancellationToken.None);
        Assert.Equal(BookingStatus.Rejected, stored.Status);
        Assert.NotNull(stored.ProcessedAt);
    }

    [Fact]
    public async Task RejectBooking_ReleaseSeats_AllowsNewBookingForSameSeat()
    {
        var testEvent = await SeedEventAsync(totalSeats: 1);

        var firstBookingDto = await BookAsync(testEvent.Id);
        Assert.Equal(0, await GetAvailableSeatsAsync(testEvent.Id));

        await RejectBookingAndReleaseSeatAsync(firstBookingDto.Id, testEvent.Id);

        var secondBookingDto = await BookAsync(testEvent.Id);

        Assert.NotEqual(firstBookingDto.Id, secondBookingDto.Id);
        Assert.Equal(0, await GetAvailableSeatsAsync(testEvent.Id));
    }

    // ---------- Конкурентность ----------

    [Fact]
    public async Task CreateBookingAsync_ConcurrentRequestsExceedingCapacity_OnlyCapacitySucceeds()
    {
        var testEvent = await SeedEventAsync(totalSeats: 5);

        // Отдельный scope (а значит и отдельный DbContext) на каждый параллельный запрос.
        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                try
                {
                    await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None);
                    return true;
                }
                catch (NoAvailableSeatsException)
                {
                    return false;
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Count(success => success));
        Assert.Equal(15, results.Count(success => !success));
        Assert.Equal(0, await GetAvailableSeatsAsync(testEvent.Id));
    }

    [Fact]
    public async Task CreateBookingAsync_ConcurrentRequestsWithinCapacity_AllBookingsHaveUniqueIds()
    {
        var testEvent = await SeedEventAsync(totalSeats: 10);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                return await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None);
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Select(r => r.Id).Distinct().Count());
        Assert.Equal(0, await GetAvailableSeatsAsync(testEvent.Id));
    }
}
