using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Domain.Enums;

namespace EventsService.Application.Interfaces.Bookings
{
    public interface IBookingService
    {
        public Task<BookingResponseDto> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken ct);
        public Task<BookingResponseDto> GetBookingByIdAsync(Guid bookingId, Guid userId, CancellationToken ct);
        public Task CancelBookingAsync(Guid bookingId, Guid userId, UserRole role, CancellationToken ct);
    }
}
