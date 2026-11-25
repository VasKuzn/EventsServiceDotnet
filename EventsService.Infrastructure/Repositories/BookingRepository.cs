using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
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

        public async Task<Booking> CreateBookingWithReservedSeatAsync(Guid eventId, Guid userId, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var bookedEvent = await dbContext.Events.FindAsync([eventId], ct)
                    ?? throw new NotFoundException($"Event with ID {eventId} not found.");

                if (!bookedEvent.TryReserveSeats())
                {
                    throw new NoAvailableSeatsException();
                }

                var booking = dbContext.Bookings.Add(Booking.Create(eventId, userId));
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                    return booking.Entity;
                }
                catch (DbUpdateConcurrencyException ex) when (ex.Entries.Any(entry => entry.Entity == bookedEvent))
                {
                }
                finally
                {
                    if (booking.State == EntityState.Added)
                    {
                        booking.State = EntityState.Detached;
                        dbContext.Entry(bookedEvent).State = EntityState.Detached;
                    }
                }
            }
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
