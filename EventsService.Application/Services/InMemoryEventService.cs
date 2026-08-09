using EventsService.Application.DataTransferObjects;
using EventsService.Application.Interfaces;

namespace EventsService.Application.Services
{
    public sealed class InMemoryEventService(IEventRepository eventRepository) : IEventService
    {
        public async Task<EventResponseDto> CreateEventAsync(CreateEventDto eventModel, CancellationToken cancellationToken)
        {
            var createdEvent = await eventRepository.CreateEventAsync(eventModel.ToEntity(), cancellationToken);
            return EventResponseDto.FromEntity(createdEvent);
        }

        public async Task<bool> DeleteEventAsync(Guid id, CancellationToken cancellationToken)
        {
            return await eventRepository.DeleteEventAsync(id, cancellationToken);
        }

        public async Task<EventResponseDto?> GetEventAsync(Guid id, CancellationToken cancellationToken)
        {
            var foundEvent = await eventRepository.GetEventAsync(id, cancellationToken);
            return foundEvent is null ? null : EventResponseDto.FromEntity(foundEvent);
        }

        public async Task<IReadOnlyList<EventResponseDto>> GetEventsAsync(CancellationToken cancellationToken)
        {
            var events = await eventRepository.GetEventsAsync(cancellationToken);
            return events.Select(EventResponseDto.FromEntity).ToList();
        }

        public async Task<EventResponseDto?> UpdateEventAsync(Guid id, UpdateEventDto eventModel, CancellationToken cancellationToken)
        {
            var updatedEvent = await eventRepository.UpdateEventAsync(eventModel.ToEntity(id), cancellationToken);
            return updatedEvent is null ? null : EventResponseDto.FromEntity(updatedEvent);
        }
    }
}