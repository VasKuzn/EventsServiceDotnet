using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Models;

namespace EventsService.Infrastructure.Repositories
{
    public sealed class InMemoryBookingRepository(List<Booking> bookings) : IBookingRepository
    {
        public async Task<Booking> CreateBookingAsync(Booking bookingEntity, CancellationToken ct)
        {
            bookings.Add(bookingEntity);

            return bookingEntity;
        }

        public async Task<bool> DeleteBookingAsync(Guid id, CancellationToken ct)
        {
            var bookingToDelete = bookings.FirstOrDefault(b => b.Id == id);

            if (bookingToDelete != null)
            {
                bookings.Remove(bookingToDelete);
                return true;
            }
            return false;
        }

        public async Task<Booking?> GetBookingAsync(Guid id, CancellationToken ct)
        {
            var bookingToFind = bookings.FirstOrDefault(b => b.Id == id);

            return bookingToFind;
        }

        public async Task<IReadOnlyList<Booking>> GetBookingsAsync(CancellationToken ct)
        {
            return bookings.ToList();
        }

        public async Task<Booking?> UpdateBookingAsync(Booking bookingEntity, CancellationToken ct)
        {
            var indexToReplace = bookings.FindIndex(e => e.Id == bookingEntity.Id);

            if (indexToReplace != -1)
            {
                bookings[indexToReplace] = bookingEntity;
                return bookingEntity;
            }
            return null;
        }
    }
}