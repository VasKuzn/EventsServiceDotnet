namespace EventsService.Domain.SystemExceptions
{
    public sealed class ActiveBookingLimitExceededException(int limit) : Exception($"The active booking limit of {limit} has been reached.")
    {
    }
}
