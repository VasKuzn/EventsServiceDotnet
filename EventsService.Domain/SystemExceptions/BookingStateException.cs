namespace EventsService.Domain.SystemExceptions
{
    public sealed class BookingStateException(string message) : Exception(message)
    {
    }
}
