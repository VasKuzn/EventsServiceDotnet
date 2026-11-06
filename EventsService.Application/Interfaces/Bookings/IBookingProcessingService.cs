namespace EventsService.Application.Interfaces.Bookings
{
    public interface IBookingProcessingService
    {
        Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken ct);
        Task ProcessBookingAsync(Guid bookingId, CancellationToken ct);
    }
}
