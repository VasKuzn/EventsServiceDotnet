using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Services;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
using EventsService.Infrastructure.Repositories;

namespace EventsService.Tests;

public class BookingSeatManagementTests
{
    private static Event CreateTestEvent(int totalSeats = 10) =>
        Event.Create(Guid.NewGuid(), "Конференция .NET", "Ежегодная конференция по разработке ПО",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), totalSeats);

    private static (IBookingService BookingService, IBookingRepository BookingRepository, List<Event> Events) CreateSut()
    {
        var events = new List<Event>();
        var bookings = new List<Booking>();
        var eventRepository = new InMemoryEventRepository(events);
        var bookingRepository = new InMemoryBookingRepository(bookings);
        var bookingService = new InMemoryBookingService(bookingRepository, eventRepository);

        return (bookingService, bookingRepository, events);
    }

    // ---------- Успешные сценарии ----------

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_DecreasesAvailableSeatsByOne()
    {
        var (bookingService, _, events) = CreateSut();
        var testEvent = CreateTestEvent(totalSeats: 10);
        events.Add(testEvent);

        await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None);

        Assert.Equal(9, testEvent.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_UpToCapacity_AllSucceedWithUniqueIds()
    {
        var (bookingService, _, events) = CreateSut();
        var testEvent = CreateTestEvent(totalSeats: 5);
        events.Add(testEvent);

        var results = new List<BookingResponseDto>();
        for (var i = 0; i < 5; i++)
        {
            results.Add(await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None));
        }

        Assert.Equal(5, results.Select(r => r.Id).Distinct().Count());
        Assert.All(results, r => Assert.Equal(BookingStatus.Pending, r.Status));
        Assert.Equal(0, testEvent.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_SeatsExhausted_NextAttemptThrowsNoAvailableSeatsException()
    {
        var (bookingService, _, events) = CreateSut();
        var testEvent = CreateTestEvent(totalSeats: 1);
        events.Add(testEvent);

        await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None));
    }

    // ---------- Неуспешные сценарии ----------

    [Fact]
    public async Task CreateBookingAsync_NonExistingEvent_ThrowsNotFoundException()
    {
        var (bookingService, _, _) = CreateSut();

        await Assert.ThrowsAsync<NotFoundException>(
            () => bookingService.CreateBookingAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateBookingAsync_NoAvailableSeats_ThrowsNoAvailableSeatsException()
    {
        var (bookingService, _, events) = CreateSut();
        var testEvent = CreateTestEvent(totalSeats: 1);
        events.Add(testEvent);

        await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(
            () => bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None));

        Assert.Equal(0, testEvent.AvailableSeats);
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
        var (bookingService, bookingRepository, events) = CreateSut();
        var testEvent = CreateTestEvent(totalSeats: 3);
        events.Add(testEvent);

        var bookingDto = await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None);
        Assert.Equal(2, testEvent.AvailableSeats);

        var booking = await bookingRepository.GetBookingAsync(bookingDto.Id, CancellationToken.None);
        booking!.Reject(DateTime.UtcNow);
        testEvent.ReleaseSeats();

        Assert.Equal(3, testEvent.AvailableSeats);
    }

    [Fact]
    public async Task RejectBooking_ReleaseSeats_AllowsNewBookingForSameSeat()
    {
        var (bookingService, bookingRepository, events) = CreateSut();
        var testEvent = CreateTestEvent(totalSeats: 1);
        events.Add(testEvent);

        var firstBookingDto = await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None);
        Assert.Equal(0, testEvent.AvailableSeats);

        var firstBooking = await bookingRepository.GetBookingAsync(firstBookingDto.Id, CancellationToken.None);
        firstBooking!.Reject(DateTime.UtcNow);
        testEvent.ReleaseSeats();

        var secondBookingDto = await bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None);

        Assert.NotEqual(firstBookingDto.Id, secondBookingDto.Id);
        Assert.Equal(0, testEvent.AvailableSeats);
    }

    // ---------- Конкурентность ----------

    [Fact]
    public async Task CreateBookingAsync_ConcurrentRequestsExceedingCapacity_OnlyCapacitySucceeds()
    {
        var (bookingService, _, events) = CreateSut();
        var testEvent = CreateTestEvent(totalSeats: 5);
        events.Add(testEvent);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
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
        Assert.Equal(0, testEvent.AvailableSeats);
    }

    [Fact]
    public async Task CreateBookingAsync_ConcurrentRequestsWithinCapacity_AllBookingsHaveUniqueIds()
    {
        var (bookingService, _, events) = CreateSut();
        var testEvent = CreateTestEvent(totalSeats: 10);
        events.Add(testEvent);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => bookingService.CreateBookingAsync(testEvent.Id, CancellationToken.None)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Select(r => r.Id).Distinct().Count());
        Assert.Equal(0, testEvent.AvailableSeats);
    }
}
