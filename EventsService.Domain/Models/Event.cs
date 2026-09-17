using EventsService.Domain.SystemExceptions;

namespace EventsService.Domain.Models
{
    public sealed class Event
    {
        public Guid Id { get; private set; }

        public string Title { get; private set; }

        public string? Description { get; private set; }

        public DateTime StartAt { get; private set; }

        public DateTime EndAt { get; private set; }

        public int TotalSeats { get; private set; }

        public int AvailableSeats { get; private set; }

        private Event(Guid id, string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
        {
            Id = id;
            Title = title;
            Description = description;
            StartAt = startAt;
            EndAt = endAt;
            TotalSeats = totalSeats;
            AvailableSeats = totalSeats;
        }

        public static Event Create(Guid id, string title, string? description, DateTime startAt, DateTime endAt, int totalSeats)
        {
            ThrowIfNotValid(title, startAt, endAt, totalSeats);
            return new Event(id, title, description, startAt, endAt, totalSeats);
        }

        private static void ThrowIfNotValid(string title, DateTime startAt, DateTime endAt, int totalSeats)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ValidationException("Title обязателен для заполнения", nameof(title));
            }
            if (endAt <= startAt)
            {
                throw new ValidationException("EndAt должен быть позже StartAt", nameof(endAt));
            }
            if (totalSeats <= 0)
            {
                throw new ValidationException("TotalSeats должен быть больше нуля", nameof(totalSeats));
            }
        }
        public bool TryReserveSeats(int count = 1)
        {
            if (AvailableSeats < count)
            {
                return false;
            }
            AvailableSeats -= count;
            return true;
        }
        public void ReleaseSeats(int count = 1)
        {
            if (AvailableSeats + count > TotalSeats)
            {
                throw new ValidationException("Попытка высвободить мест больше, чем мест всего", nameof(AvailableSeats));
            }
            AvailableSeats += count;
        }
    }
}
