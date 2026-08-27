namespace EventsService.Domain.SystemExceptions
{
    public class ValidationException(string message, string? propertyName = null) : Exception(message)
    {
        public string? PropertyName { get; } = propertyName;
    }
}