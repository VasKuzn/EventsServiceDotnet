using EventsService.Application.Interfaces;
using EventsService.Domain.Models;

namespace EventsService.Infrastructure.Repositories
{
    public class InMemoryEventRepository(List<Event> events) : IEventRepository
    {
        private List<Event> _events { get; } = events;

        public async Task<Event> CreateEventAsync(Event eventEntity, CancellationToken ct)
        {
            _events.Add(eventEntity);

            return eventEntity;
        }

        public async Task<bool> DeleteEventAsync(Guid id, CancellationToken ct)
        {
            var eventToDelete = _events.FirstOrDefault(e => e.Id == id);

            if (eventToDelete != null)
            {
                _events.Remove(eventToDelete);
                return true;
            }
            return false;
        }

        public async Task<Event?> GetEventAsync(Guid id, CancellationToken ct)
        {
            var eventToFind = _events.FirstOrDefault(e => e.Id == id);

            return eventToFind;
        }

        public async Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken ct)
        {
            return _events.ToList();
        }

        public async Task<Event?> UpdateEventAsync(Event eventEntity, CancellationToken ct)
        {
            var indexToReplace = _events.FindIndex(e => e.Id == eventEntity.Id);

            if (indexToReplace != -1)
            {
                _events[indexToReplace] = eventEntity;
                return eventEntity;
            }
            return null;
        }
    }
}