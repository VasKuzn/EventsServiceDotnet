using EventsService.Domain.Models;

namespace EventsService.Application.Interfaces.Bookings
{
    public interface IBookingRepository
    {
        public Task<Booking?> GetBookingAsync(Guid id, CancellationToken ct);
        public Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken ct);
        public Task<Booking> CreateBookingAsync(Booking bookingEntity, CancellationToken ct);
        public Task<Booking> CreateBookingWithReservedSeatAsync(Guid eventId, Guid userId, Action<Event, int> validateBooking, CancellationToken ct);
        public Task CancelBookingAndReleaseSeatAsync(Guid bookingId, Func<Booking, bool> cancelBooking, CancellationToken ct);
        public Task UpdateBookingAsync(Booking bookingEntity, CancellationToken ct);
    }
}
