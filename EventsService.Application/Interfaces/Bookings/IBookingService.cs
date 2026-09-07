using EventsService.Application.DataTransferObjects.Bookings;

namespace EventsService.Application.Interfaces.Bookings
{
    public interface IBookingService
    {
        public Task<BookingResponseDto> CreateBookingAsync(Guid eventId, CancellationToken ct);
        public Task<BookingResponseDto> GetBookingByIdAsync(Guid bookingId, CancellationToken ct);
    }
}