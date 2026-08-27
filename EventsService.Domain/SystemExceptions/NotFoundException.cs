namespace EventsService.Domain.SystemExceptions
{
    public class NotFoundException(string message) : Exception(message)
    {
    }
}