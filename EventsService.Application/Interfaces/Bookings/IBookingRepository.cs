using EventsService.Domain.Models;

namespace EventsService.Application.Interfaces.Bookings
{
    public interface IBookingRepository
    {
        public Task<Booking?> GetBookingAsync(Guid id, CancellationToken ct);
        public Task<IReadOnlyList<Booking>> GetBookingsAsync(CancellationToken ct);
        public Task<Booking> CreateBookingAsync(Booking bookingEntity, CancellationToken ct);
        public Task<Booking?> UpdateBookingAsync(Booking bookingEntity, CancellationToken ct);
        public Task<bool> DeleteBookingAsync(Guid id, CancellationToken ct);
    }
}