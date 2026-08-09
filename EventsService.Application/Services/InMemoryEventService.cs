using EventsService.Application.DataTransferObjects;
using EventsService.Application.Interfaces;
using EventsService.Domain.Models;

namespace EventsService.Application.Services
{
    public class InMemoryEventService(IEventRepository eventRepository) : IEventService
    {
        public IEventRepository EventRepository { get; } = eventRepository;

        public async Task<Event> CreateEventAsync(CreateEventDto eventModel, CancellationToken cancellationToken)
        {
            var eventToCreate = new Event
            {
                Id = Guid.NewGuid(),
                Title = eventModel.Title,
                Description = eventModel.Description,
                StartAt = eventModel.StartAt,
                EndAt = eventModel.EndAt
            };
            return await EventRepository.CreateEventAsync(eventToCreate, cancellationToken);
        }

        public async Task<bool> DeleteEventAsync(Guid id, CancellationToken cancellationToken)
        {
            return await EventRepository.DeleteEventAsync(id, cancellationToken);
        }

        public async Task<Event?> GetEventAsync(Guid id, CancellationToken cancellationToken)
        {
            return await EventRepository.GetEventAsync(id, cancellationToken);
        }

        public async Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken cancellationToken)
        {
            return await EventRepository.GetEventsAsync(cancellationToken);
        }

        public async Task<Event?> UpdateEventAsync(Guid id, UpdateEventDto eventModel, CancellationToken cancellationToken)
        {
            var eventToUpdate = new Event
            {
                Id = id,
                Title = eventModel.Title,
                Description = eventModel.Description,
                StartAt = eventModel.StartAt,
                EndAt = eventModel.EndAt
            };
            return await EventRepository.UpdateEventAsync(eventToUpdate, cancellationToken);
        }
    }
}