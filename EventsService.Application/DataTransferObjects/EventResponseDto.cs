using EventsService.Domain.Models;

namespace EventsService.Application.DataTransferObjects
{
    public record EventResponseDto
    {
        public required Guid Id { get; init; }

        public required string Title { get; init; }

        public string? Description { get; init; }

        public required DateTime StartAt { get; init; }

        public required DateTime EndAt { get; init; }

        public static EventResponseDto FromEntity(Event entity) => new()
        {
            Id = entity.Id,
            Title = entity.Title,
            Description = entity.Description,
            StartAt = entity.StartAt,
            EndAt = entity.EndAt
        };
    }
}
