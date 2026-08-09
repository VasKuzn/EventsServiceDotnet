using EventsService.Application.Interfaces;
using EventsService.Domain.Models;

namespace EventsService.Infrastructure.Repositories
{
    public sealed class InMemoryEventRepository(List<Event> events) : IEventRepository
    {
        public async Task<Event> CreateEventAsync(Event eventEntity, CancellationToken ct)
        {
            events.Add(eventEntity);

            return eventEntity;
        }

        public async Task<bool> DeleteEventAsync(Guid id, CancellationToken ct)
        {
            var eventToDelete = events.FirstOrDefault(e => e.Id == id);

            if (eventToDelete != null)
            {
                events.Remove(eventToDelete);
                return true;
            }
            return false;
        }

        public async Task<Event?> GetEventAsync(Guid id, CancellationToken ct)
        {
            var eventToFind = events.FirstOrDefault(e => e.Id == id);

            return eventToFind;
        }

        public async Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken ct)
        {
            return events.ToList();
        }

        public async Task<Event?> UpdateEventAsync(Event eventEntity, CancellationToken ct)
        {
            var indexToReplace = events.FindIndex(e => e.Id == eventEntity.Id);

            if (indexToReplace != -1)
            {
                events[indexToReplace] = eventEntity;
                return eventEntity;
            }
            return null;
        }
    }
}