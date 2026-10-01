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

        public async Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken ct)
        {
            return await dbContext.Events.ToListAsync(ct);
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