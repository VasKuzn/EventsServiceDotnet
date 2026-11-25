using EventsService.Domain.Enums;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Domain.Models
{
    public sealed class Booking
    {
        public Guid Id { get; private set; }

        public Guid EventId { get; private set; }

        public BookingStatus Status { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? ProcessedAt { get; private set; }

        public Event Event { get; private set; } = null!;

        private Booking(Guid id, Guid eventId, BookingStatus status, DateTime createdAt, DateTime? processedat)
        {
            Id = id;
            EventId = eventId;
            Status = status;
            CreatedAt = createdAt;
            ProcessedAt = processedat;
        }
        private Booking() { }

        public static Booking Create(Guid eventId)
        {
            return new Booking(Guid.NewGuid(), eventId, BookingStatus.Pending, DateTime.UtcNow, null);
        }

        public void Confirm(DateTime processedAt)
        {
            if (Status != BookingStatus.Pending)
                throw new BookingStateException($"Cannot confirm booking {Id} in status {Status}.");

            Status = BookingStatus.Confirmed;
            ProcessedAt = processedAt;
        }

        public void Reject(DateTime processedAt)
        {
            if (Status != BookingStatus.Pending)
                throw new BookingStateException($"Cannot reject booking {Id} in status {Status}.");

            Status = BookingStatus.Rejected;
            ProcessedAt = processedAt;
        }
    }
}
