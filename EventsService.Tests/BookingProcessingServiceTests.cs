using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Application.Services;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EventsService.Tests;

public class BookingProcessingServiceTests
{
    private readonly Mock<IBookingRepository> _bookingRepository = new(MockBehavior.Strict);
    private readonly Mock<IEventRepository> _eventRepository = new(MockBehavior.Strict);
    private readonly Mock<IEventSeatReleaser> _eventSeatReleaser = new(MockBehavior.Strict);

    private BookingProcessingService CreateService() => new(
        _bookingRepository.Object,
        _eventRepository.Object,
        _eventSeatReleaser.Object,
        NullLogger<BookingProcessingService>.Instance);

    private void SetUpBooking(Booking booking)
    {
        _bookingRepository.Setup(r => r.GetBookingAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);
    }

    [Fact]
    public async Task ProcessBookingAsync_PendingBooking_ConfirmsAndPersistsBooking()
    {
        var eventEntity = Event.Create(Guid.NewGuid(), "Conference", null,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 10);
        var booking = Booking.Create(eventEntity.Id);
        SetUpBooking(booking);
        _eventRepository.Setup(r => r.GetEventAsync(eventEntity.Id, CancellationToken.None))
            .ReturnsAsync(eventEntity);
        _bookingRepository.Setup(r => r.UpdateBookingAsync(booking, CancellationToken.None))
            .Returns(Task.CompletedTask);

        await CreateService().ProcessBookingAsync(booking.Id, CancellationToken.None);

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        _bookingRepository.Verify(r => r.UpdateBookingAsync(booking, CancellationToken.None), Times.Once);
        _eventSeatReleaser.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessBookingAsync_UnknownBooking_DoesNotChangeAnything()
    {
        var bookingId = Guid.NewGuid();
        _bookingRepository.Setup(r => r.GetBookingAsync(bookingId, CancellationToken.None))
            .ReturnsAsync((Booking?)null);

        await CreateService().ProcessBookingAsync(bookingId, CancellationToken.None);

        _bookingRepository.Verify(r => r.UpdateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventRepository.VerifyNoOtherCalls();
        _eventSeatReleaser.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    public async Task ProcessBookingAsync_AlreadyProcessedBooking_DoesNotProcessAgain(BookingStatus status)
    {
        var booking = Booking.Create(Guid.NewGuid());
        if (status == BookingStatus.Confirmed)
            booking.Confirm(DateTime.UtcNow);
        else
            booking.Reject(DateTime.UtcNow);
        SetUpBooking(booking);

        await CreateService().ProcessBookingAsync(booking.Id, CancellationToken.None);

        Assert.Equal(status, booking.Status);
        _bookingRepository.Verify(r => r.UpdateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventRepository.VerifyNoOtherCalls();
        _eventSeatReleaser.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessBookingAsync_MissingEvent_RejectsBookingWithoutReleasingSeats()
    {
        var booking = Booking.Create(Guid.NewGuid());
        SetUpBooking(booking);
        _eventRepository.Setup(r => r.GetEventAsync(booking.EventId, CancellationToken.None))
            .ReturnsAsync((Event?)null);
        _bookingRepository.Setup(r => r.UpdateBookingAsync(booking, CancellationToken.None))
            .Returns(Task.CompletedTask);

        await CreateService().ProcessBookingAsync(booking.Id, CancellationToken.None);

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        _bookingRepository.Verify(r => r.UpdateBookingAsync(booking, CancellationToken.None), Times.Once);
        _eventSeatReleaser.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessBookingAsync_ProcessingFailure_RejectsBookingAndReleasesSeats()
    {
        var booking = Booking.Create(Guid.NewGuid());
        SetUpBooking(booking);
        _eventRepository.Setup(r => r.GetEventAsync(booking.EventId, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("Event lookup failed"));
        var sequence = new MockSequence();
        _bookingRepository.InSequence(sequence)
            .Setup(r => r.UpdateBookingAsync(booking, CancellationToken.None))
            .Returns(Task.CompletedTask);
        _eventSeatReleaser.InSequence(sequence)
            .Setup(r => r.ReleaseSeatsAsync(booking.EventId, CancellationToken.None))
            .Returns(Task.CompletedTask);

        await CreateService().ProcessBookingAsync(booking.Id, CancellationToken.None);

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        _bookingRepository.Verify(r => r.UpdateBookingAsync(booking, CancellationToken.None), Times.Once);
        _eventSeatReleaser.Verify(r => r.ReleaseSeatsAsync(booking.EventId, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ProcessBookingAsync_Cancellation_DoesNotRejectBookingOrReleaseSeats()
    {
        var booking = Booking.Create(Guid.NewGuid());
        SetUpBooking(booking);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _eventRepository.Setup(r => r.GetEventAsync(booking.EventId, cancellation.Token))
            .Returns(Task.FromCanceled<Event?>(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService().ProcessBookingAsync(booking.Id, cancellation.Token));

        Assert.Equal(BookingStatus.Pending, booking.Status);
        _bookingRepository.Verify(r => r.UpdateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventSeatReleaser.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ProcessBookingAsync_ConfirmationSaveFails_PropagatesOriginalError()
    {
        var eventEntity = Event.Create(Guid.NewGuid(), "Conference", null,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 10);
        var booking = Booking.Create(eventEntity.Id);
        SetUpBooking(booking);
        _eventRepository.Setup(r => r.GetEventAsync(eventEntity.Id, CancellationToken.None))
            .ReturnsAsync(eventEntity);
        var failure = new InvalidOperationException("Saving failed");
        _bookingRepository.Setup(r => r.UpdateBookingAsync(booking, CancellationToken.None))
            .ThrowsAsync(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService().ProcessBookingAsync(booking.Id, CancellationToken.None));

        Assert.Same(failure, exception);
        _bookingRepository.Verify(r => r.UpdateBookingAsync(booking, CancellationToken.None), Times.Once);
        _eventSeatReleaser.VerifyNoOtherCalls();
    }
}
