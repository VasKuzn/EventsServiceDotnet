using EventsService.Domain.Models;

namespace EventsService.IntegrationTests.Infrastructure;

public static class TestData
{
    public static readonly DateTime BaseDate = new(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    public static Event CreateEvent(
        string title = "Event",
        DateTime? startAt = null,
        TimeSpan? duration = null,
        int totalSeats = 10,
        string? description = null)
    {
        var start = startAt ?? BaseDate;
        return Event.Create(Guid.NewGuid(), title, description, start, start + (duration ?? TimeSpan.FromHours(2)), totalSeats);
    }
}
