using EventsService.Domain.Enums;

namespace EventsService.Domain.Models
{
    public sealed class Booking
    {
        public Guid Id { get; private set; }

        public Guid EventId { get; private set; }

        public BookingStatus Status { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? ProcessedAt { get; private set; }

        private Booking(Guid id, Guid eventId, BookingStatus status, DateTime createdAt, DateTime? processedat)
        {
            Id = id;
            EventId = eventId;
            Status = status;
            CreatedAt = createdAt;
            ProcessedAt = processedat;
        }

        public static Booking Create(Guid eventId)
        {
            return new Booking(Guid.NewGuid(), eventId, BookingStatus.Pending, DateTime.UtcNow, null);
        }

        public void Confirm(DateTime processedAt)
        {
            if (Status != BookingStatus.Pending)
                throw new InvalidOperationException($"Cannot confirm booking {Id} in status {Status}.");

            Status = BookingStatus.Confirmed;
            ProcessedAt = processedAt;
        }

        public void Reject(DateTime processedAt)
        {
            if (Status != BookingStatus.Pending)
                throw new InvalidOperationException($"Cannot reject booking {Id} in status {Status}.");

            Status = BookingStatus.Rejected;
            ProcessedAt = processedAt;
        }

        /*
        public static Booking Create(Guid id, Guid eventId, BookingStatus status, DateTime createdAt, DateTime? processedat)
        {
            return new Booking(id, eventId, status, createdAt, processedat);
        }
        */

    }
}