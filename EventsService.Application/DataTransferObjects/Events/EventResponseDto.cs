using EventsService.Domain.Models;

namespace EventsService.Application.DataTransferObjects.Events
{
    public record EventResponseDto
    {
        public required Guid Id { get; init; }

        public required string Title { get; init; }

        public string? Description { get; init; }

        public required DateTime StartAt { get; init; }

        public required DateTime EndAt { get; init; }

        public required int TotalSeats { get; init; }

        public required int AvailableSeats { get; init; }

        public static EventResponseDto FromEntity(Event entity) => new()
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            StartAt = entity.StartAt,
            EndAt = entity.EndAt,
            TotalSeats = entity.TotalSeats,
            AvailableSeats = entity.AvailableSeats
        };
    }
}
