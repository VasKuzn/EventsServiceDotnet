using EventsService.Domain.Enums;
using EventsService.Domain.Models;

namespace EventsService.Application.DataTransferObjects.Bookings
{
    public record BookingResponseDto
    {
        public Guid Id { get; private set; }

        public Guid EventId { get; private set; }

        public BookingStatus Status { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? ProcessedAt { get; private set; }

        public static BookingResponseDto FromEntity(Booking entity) => new()
        {
            Id = entity.Id,
            EventId = entity.EventId,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            ProcessedAt = entity.ProcessedAt
        };
    }
}