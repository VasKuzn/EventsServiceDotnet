using EventsService.Application.DataTransferObjects;

namespace EventsService.Application.Interfaces
{
    public interface IEventService
    {
        public Task<EventResponseDto?> GetEventAsync(Guid id, CancellationToken cancellationToken);
        public Task<IReadOnlyList<EventResponseDto>> GetEventsAsync(CancellationToken cancellationToken);
        public Task<EventResponseDto> CreateEventAsync(CreateEventDto eventModel, CancellationToken cancellationToken);
        public Task<EventResponseDto?> UpdateEventAsync(Guid id, UpdateEventDto eventModel, CancellationToken cancellationToken);
        public Task<bool> DeleteEventAsync(Guid id, CancellationToken cancellationToken);
    }
}