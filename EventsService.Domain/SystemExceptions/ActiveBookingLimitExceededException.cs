namespace EventsService.Domain.SystemExceptions
{
    public sealed class ActiveBookingLimitExceededException() : Exception("The active booking limit has been reached.")
    {
    }
}
