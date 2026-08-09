using EventsService.Application.DataTransferObjects;
using EventsService.Domain.Models;

namespace EventsService.Application.Interfaces
{
    public interface IEventService
    {
        public Task<Event?> GetEventAsync(Guid id, CancellationToken cancellationToken);
        public Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken cancellationToken);
        public Task<Event> CreateEventAsync(CreateEventDto eventModel, CancellationToken cancellationToken);
        public Task<Event?> UpdateEventAsync(Guid id, UpdateEventDto eventModel, CancellationToken cancellationToken);
        public Task<bool> DeleteEventAsync(Guid id, CancellationToken cancellationToken);
    }
}