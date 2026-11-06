using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Enums;
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

        public async Task<Booking?> GetBookingAsync(Guid id, CancellationToken ct)
        {
            return await dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);
        }

        public async Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken ct)
        {
            return await dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.Status == BookingStatus.Pending)
                .Select(b => b.Id)
                .ToListAsync(ct);
        }

        public async Task UpdateBookingAsync(Booking bookingEntity, CancellationToken ct)
        {
            dbContext.Bookings.Update(bookingEntity);

            await dbContext.SaveChangesAsync(ct);
        }
    }
}