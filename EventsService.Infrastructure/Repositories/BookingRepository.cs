using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Models;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventsService.Infrastructure.Repositories
{
    public sealed class BookingRepository(AppDbContext dbContext) : IBookingRepository
    {
        public async Task<Booking> CreateBookingAsync(Booking bookingEntity, CancellationToken ct)
        {
            await dbContext.AddAsync(bookingEntity, ct);

            await dbContext.SaveChangesAsync(ct);

            return bookingEntity;
        }

        public async Task<bool> DeleteBookingAsync(Guid id, CancellationToken ct)
        {
            var deleted = await dbContext.Bookings
                .Where(e => e.Id == id)
                .ExecuteDeleteAsync(ct);

            await dbContext.SaveChangesAsync(ct);

            return deleted > 0;
        }

        public async Task<Booking?> GetBookingAsync(Guid id, CancellationToken ct)
        {
            return await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);
        }

        public async Task<IReadOnlyList<Booking>> GetBookingsAsync(CancellationToken ct)
        {
            return await dbContext.Bookings.ToListAsync(ct);
        }

        public async Task<Booking?> UpdateBookingAsync(Booking bookingEntity, CancellationToken ct)
        {
            dbContext.Bookings.Update(bookingEntity);

            await dbContext.SaveChangesAsync(ct);

            return bookingEntity;
        }
    }
}