namespace EventsService.Domain.Models
{
    public sealed class Event
    {
        public Guid Id { get; private set; }

        public string Title { get; private set; }

        public string? Description { get; private set; }

        public DateTime StartAt { get; private set; }

        public DateTime EndAt { get; private set; }

        private Event(Guid id, string title, string? description, DateTime startAt, DateTime endAt)
        {
            Id = id;
            Title = title;
            Description = description;
            StartAt = startAt;
            EndAt = endAt;
        }

        public static Event Create(Guid id, string title, string? description, DateTime startAt, DateTime endAt)
        {
            ThrowIfNotValid(title, startAt, endAt);
            return new Event(id, title, description, startAt, endAt);
        }

        private static void ThrowIfNotValid(string title, DateTime startAt, DateTime endAt)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Title обязателен для заполнения", nameof(title));
            }
            if (endAt <= startAt)
            {
                throw new ArgumentException("EndAt должен быть позже StartAt", nameof(endAt));
            }
        }
    }
}
