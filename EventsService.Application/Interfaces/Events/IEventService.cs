using EventsService.Application.DataTransferObjects.Events;

namespace EventsService.Application.Interfaces.Events
{
    public interface IEventService
    {
        public Task<EventResponseDto?> GetEventAsync(Guid id, CancellationToken cancellationToken);
        public Task<PaginatedResult<EventResponseDto>> GetEventsAsync(string? title, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default);
        public Task<EventResponseDto> CreateEventAsync(CreateEventDto eventModel, CancellationToken cancellationToken);
        public Task<EventResponseDto?> UpdateEventAsync(Guid id, UpdateEventDto eventModel, CancellationToken cancellationToken);
        public Task<bool> DeleteEventAsync(Guid id, CancellationToken cancellationToken);
    }
}