using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Models;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventsService.Infrastructure.Repositories
{
    public sealed class EventRepository(AppDbContext dbContext) : IEventRepository
    {
        public async Task<Event> CreateEventAsync(Event eventEntity, CancellationToken ct)
        {
            await dbContext.AddAsync(eventEntity, ct);

            await dbContext.SaveChangesAsync(ct);

            return eventEntity;
        }

        public async Task<bool> DeleteEventAsync(Guid id, CancellationToken ct)
        {
            var eventEntity = await dbContext.Events.FindAsync([id], ct);
            if (eventEntity is null)
            {
                return false;
            }

            dbContext.Events.Remove(eventEntity);
            await dbContext.SaveChangesAsync(ct);

            return true;
        }

        public async Task<Event?> GetEventAsync(Guid id, CancellationToken ct)
        {
            return await dbContext.Events.FirstOrDefaultAsync(e => e.Id == id, ct);
        }

        public async Task<(IReadOnlyList<Event> Items, int TotalCount)> GetEventsAsync(
            string? title, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct)
        {
            var query = dbContext.Events.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(title))
            {
                var loweredTitle = title.ToLower();
                query = query.Where(e => e.Title.ToLower().Contains(loweredTitle));
            }
            if (from.HasValue)
            {
                query = query.Where(e => e.StartAt >= from.Value);
            }
            if (to.HasValue)
            {
                query = query.Where(e => e.EndAt <= to.Value);
            }

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .OrderBy(e => e.StartAt)
                .ThenBy(e => e.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public async Task<Event?> UpdateEventAsync(Event eventEntity, CancellationToken ct)
        {
            var existingEvent = await dbContext.Events.FindAsync([eventEntity.Id], ct);
            if (existingEvent is null)
            {
                return null;
            }

            dbContext.Entry(existingEvent).CurrentValues.SetValues(eventEntity);

            await dbContext.SaveChangesAsync(ct);

            return existingEvent;
        }
    }
}