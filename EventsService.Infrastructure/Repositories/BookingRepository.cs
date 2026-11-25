using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using System.Data;

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

        public async Task<Booking> CreateBookingWithReservedSeatAsync(Guid eventId, Guid userId, Action<Event, int> validateBooking, CancellationToken ct)
        {
            await using var transaction = dbContext.Database.IsNpgsql()
                ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)
                : null;

            if (transaction is not null)
                await dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM users WHERE id = {userId} FOR NO KEY UPDATE", ct);

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var bookedEvent = await dbContext.Events.FindAsync([eventId], ct)
                    ?? throw new NotFoundException($"Event with ID {eventId} not found.");

                var activeBookingCount = await dbContext.Bookings.CountAsync(b => b.UserId == userId &&
                    (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed), ct);
                validateBooking(bookedEvent, activeBookingCount);

                if (!bookedEvent.TryReserveSeats())
                {
                    throw new NoAvailableSeatsException();
                }

                var booking = dbContext.Bookings.Add(Booking.Create(eventId, userId));
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                    if (transaction is not null)
                        await transaction.CommitAsync(ct);
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

        public async Task CancelBookingAndReleaseSeatAsync(Guid bookingId, Func<Booking, bool> cancelBooking, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var booking = await dbContext.Bookings.FindAsync([bookingId], ct)
                    ?? throw new NotFoundException($"Booking with ID {bookingId} not found.");
                var bookedEvent = await dbContext.Events.FindAsync([booking.EventId], ct)
                    ?? throw new NotFoundException($"Booking with ID {bookingId} not found.");

                if (!cancelBooking(booking))
                    return;

                try
                {
                    bookedEvent.ReleaseSeats();
                    await dbContext.SaveChangesAsync(ct);
                    return;
                }
                catch (DbUpdateConcurrencyException ex) when (ex.Entries.All(entry => entry.Entity == bookedEvent || entry.Entity == booking))
                {
                }
                finally
                {
                    var bookingEntry = dbContext.Entry(booking);
                    if (bookingEntry.State == EntityState.Modified)
                    {
                        bookingEntry.State = EntityState.Detached;
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
            var wasDetached = dbContext.Entry(bookingEntity).State == EntityState.Detached;
            var entry = dbContext.Bookings.Update(bookingEntity);
            if (wasDetached && bookingEntity.Status is (BookingStatus.Confirmed or BookingStatus.Rejected))
                entry.Property(b => b.Status).OriginalValue = BookingStatus.Pending;

            await dbContext.SaveChangesAsync(ct);
        }
    }
}
