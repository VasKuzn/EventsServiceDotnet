using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Application.Services;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
using Moq;

namespace EventsService.Tests;

public class InMemoryBookingServiceTests
{
    private readonly Mock<IBookingRepository> _bookingRepositoryMock;
    private readonly Mock<IEventRepository> _eventRepositoryMock;
    private readonly InMemoryBookingService _bookingService;

    public InMemoryBookingServiceTests()
    {
        _bookingRepositoryMock = new Mock<IBookingRepository>();
        _eventRepositoryMock = new Mock<IEventRepository>();
        _bookingService = new InMemoryBookingService(_bookingRepositoryMock.Object, _eventRepositoryMock.Object);
    }

    private static Event CreateExistingEvent(Guid eventId) =>
        Event.Create(eventId, "Конференция .NET", "Ежегодная конференция по разработке ПО",
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));

    [Fact]
    public async Task CreateBookingAsync_ExistingEvent_ReturnsPendingBooking()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        Booking? capturedEntity = null;

        _eventRepositoryMock
            .Setup(r => r.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExistingEvent(eventId));

        _bookingRepositoryMock
            .Setup(r => r.CreateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((b, _) => capturedEntity = b)
            .ReturnsAsync((Booking b, CancellationToken _) => b);

        // Act
        var result = await _bookingService.CreateBookingAsync(eventId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Null(result.ProcessedAt);
        Assert.NotNull(capturedEntity);

        _bookingRepositoryMock.Verify(
            r => r.CreateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_CalledTwiceForSameEvent_CreatesBookingsWithUniqueIds()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        _eventRepositoryMock
            .Setup(r => r.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateExistingEvent(eventId));

        _bookingRepositoryMock
            .Setup(r => r.CreateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking b, CancellationToken _) => b);

        // Act
        var first = await _bookingService.CreateBookingAsync(eventId, CancellationToken.None);
        var second = await _bookingService.CreateBookingAsync(eventId, CancellationToken.None);

        // Assert
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(eventId, first.EventId);
        Assert.Equal(eventId, second.EventId);

        _bookingRepositoryMock.Verify(
            r => r.CreateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task GetBookingByIdAsync_ExistingId_ReturnsCorrectInfo()
    {
        // Arrange
        var booking = Booking.Create(Guid.NewGuid());

        _bookingRepositoryMock
            .Setup(r => r.GetBookingAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        // Act
        var result = await _bookingService.GetBookingByIdAsync(booking.Id, CancellationToken.None);

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
        var booking = Booking.Create(Guid.NewGuid());
        var processedAt = DateTime.UtcNow;
        booking.Confirm(processedAt);

        _bookingRepositoryMock
            .Setup(r => r.GetBookingAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        // Act
        var result = await _bookingService.GetBookingByIdAsync(booking.Id, CancellationToken.None);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.Equal(processedAt, result.ProcessedAt);
    }

    [Fact]
    public async Task GetBookingByIdAsync_AfterReject_ReflectsRejectedStatus()
    {
        // Arrange
        var booking = Booking.Create(Guid.NewGuid());
        var processedAt = DateTime.UtcNow;
        booking.Reject(processedAt);

        _bookingRepositoryMock
            .Setup(r => r.GetBookingAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        // Act
        var result = await _bookingService.GetBookingByIdAsync(booking.Id, CancellationToken.None);

        // Assert
        Assert.Equal(BookingStatus.Rejected, result.Status);
        Assert.Equal(processedAt, result.ProcessedAt);
    }

    [Fact]
    public async Task CreateBookingAsync_NonExistingEvent_ThrowsNotFoundException()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        _eventRepositoryMock
            .Setup(r => r.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _bookingService.CreateBookingAsync(eventId, CancellationToken.None));

        _bookingRepositoryMock.Verify(
            r => r.CreateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_DeletedEvent_ThrowsNotFoundException()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        _eventRepositoryMock
            .Setup(r => r.GetEventAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _bookingService.CreateBookingAsync(eventId, CancellationToken.None));

        _bookingRepositoryMock.Verify(
            r => r.CreateBookingAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetBookingByIdAsync_NonExistingId_ThrowsNotFoundException()
    {
        // Arrange
        var bookingId = Guid.NewGuid();

        _bookingRepositoryMock
            .Setup(r => r.GetBookingAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _bookingService.GetBookingByIdAsync(bookingId, CancellationToken.None));
    }
}
