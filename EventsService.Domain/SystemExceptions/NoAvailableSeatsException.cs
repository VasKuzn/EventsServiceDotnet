namespace EventsService.Domain.SystemExceptions
{
    public class NoAvailableSeatsException() : Exception("No available seats for this event")
    {
    }
}
