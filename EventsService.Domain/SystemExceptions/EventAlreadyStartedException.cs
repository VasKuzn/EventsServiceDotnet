namespace EventsService.Domain.SystemExceptions
{
    public sealed class EventAlreadyStartedException() : Exception("The event has already started.")
    {
    }
}
