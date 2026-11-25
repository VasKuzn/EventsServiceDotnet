namespace EventsService.Application.Interfaces.Events
{
    public interface IEventSeatReleaser
    {
        Task ReleaseSeatsAsync(Guid eventId, CancellationToken ct);
    }
}
