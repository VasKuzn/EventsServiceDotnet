using EventsService.Application.DataTransferObjects;
using EventsService.Application.Interfaces;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;

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
            var events = await eventRepository.GetEventsAsync(cancellationToken);

            IEnumerable<Event> filteredEvents = events;

            if (!string.IsNullOrWhiteSpace(title))
            {
                filteredEvents = filteredEvents.Where(e => e.Title.Contains(title, StringComparison.OrdinalIgnoreCase));
            }
            if (from.HasValue)
            {
                filteredEvents = filteredEvents.Where(e => e.StartAt >= from.Value);
            }
            if (to.HasValue)
            {
                filteredEvents = filteredEvents.Where(e => e.EndAt <= to.Value);
            }

            var totalCount = filteredEvents.Count();

            var pagedEvents = filteredEvents
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(EventResponseDto.FromEntity)
                .ToList();

            return new PaginatedResult<EventResponseDto>
            {
                Items = pagedEvents,
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