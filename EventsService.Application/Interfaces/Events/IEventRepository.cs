using EventsService.Domain.Models;

namespace EventsService.Application.Interfaces.Events
{
    public interface IEventRepository
    {
        public Task<Event?> GetEventAsync(Guid id, CancellationToken ct);
        public Task<(IReadOnlyList<Event> Items, int TotalCount)> GetEventsAsync(string? title, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct);
        public Task<Event> CreateEventAsync(Event eventEntity, CancellationToken ct);
        public Task<Event?> UpdateEventAsync(Event eventEntity, CancellationToken ct);
        public Task<bool> DeleteEventAsync(Guid id, CancellationToken ct);
    }
}