using EventsService.Domain.Enums;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Domain.Models
{
    public sealed class Booking
    {
        public static readonly Guid LegacyOwnerId = new("237c97b3-323f-4abc-90d9-b4ec63cb43dd");

        public Guid Id { get; private set; }

        public Guid EventId { get; private set; }

        public Guid UserId { get; private set; }

        public BookingStatus Status { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public DateTime? ProcessedAt { get; private set; }

        public Event Event { get; private set; } = null!;

        public User User { get; private set; } = null!;

        private Booking(Guid id, Guid eventId, Guid userId, BookingStatus status, DateTime createdAt, DateTime? processedat)
        {
            Id = id;
            EventId = eventId;
            UserId = userId;
            Status = status;
            CreatedAt = createdAt;
            ProcessedAt = processedat;
        }
        private Booking() { }

        public static Booking Create(Guid eventId, Guid userId)
        {
            return new Booking(Guid.NewGuid(), eventId, userId, BookingStatus.Pending, DateTime.UtcNow, null);
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

        public void Cancel(DateTime processedAt)
        {
            if (Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
                throw new BookingStateException($"Cannot cancel booking {Id} in status {Status}.");

            Status = BookingStatus.Cancelled;
            ProcessedAt = processedAt;
        }
    }
}
