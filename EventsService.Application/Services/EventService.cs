using EventsService.Application.DataTransferObjects.Events;
using EventsService.Application.Interfaces.Events;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Application.Services
{
    public sealed class EventService(IEventRepository eventRepository) : IEventService
    {
        public async Task<EventResponseDto> CreateEventAsync(CreateEventDto eventModel, CancellationToken cancellationToken)
        {
            var createdEvent = await eventRepository.CreateEventAsync(eventModel.ToEntity(), cancellationToken);
            return EventResponseDto.FromEntity(createdEvent);
        }

        public async Task<bool> DeleteEventAsync(Guid id, CancellationToken cancellationToken)
        {
            var deleted = await eventRepository.DeleteEventAsync(id, cancellationToken);
            return deleted ? true : throw new NotFoundException($"Сущность с Id: {id} не найдена");
        }

        public async Task<EventResponseDto?> GetEventAsync(Guid id, CancellationToken cancellationToken)
        {
            var foundEvent = await eventRepository.GetEventAsync(id, cancellationToken);
            return foundEvent is null ? throw new NotFoundException($"Сущность с Id: {id} не найдена") : EventResponseDto.FromEntity(foundEvent);
        }

        public async Task<PaginatedResult<EventResponseDto>> GetEventsAsync(string? title = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var (events, totalCount) = await eventRepository.GetEventsAsync(title, from, to, page, pageSize, cancellationToken);

            return new PaginatedResult<EventResponseDto>
            {
                Items = events.Select(EventResponseDto.FromEntity).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<EventResponseDto?> UpdateEventAsync(Guid id, UpdateEventDto eventModel, CancellationToken cancellationToken)
        {
            var updatedEvent = await eventRepository.UpdateEventAsync(eventModel.ToEntity(id), cancellationToken);
            return updatedEvent is null ? throw new NotFoundException($"Сущность с Id: {id} не найдена") : EventResponseDto.FromEntity(updatedEvent);
        }
    }
}