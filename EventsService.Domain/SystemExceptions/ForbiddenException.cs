namespace EventsService.Domain.SystemExceptions
{
    public sealed class ForbiddenException() : Exception("You do not have permission to perform this operation.")
    {
    }
}
