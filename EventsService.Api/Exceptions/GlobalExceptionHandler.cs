using EventsService.Domain.SystemExceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EventsService.Api.Exceptions
{
    public class GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment,
        IProblemDetailsService problemDetailsService) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var exceptionMessage = exception.Message;
            logger.LogError(
                exception,
                "Error Message: {exceptionMessage}, Time of occurrence {time}",
                exceptionMessage, DateTime.UtcNow);

            var statusCode = MapStatusCode(exception);
            httpContext.Response.StatusCode = statusCode;

            var detail = statusCode == StatusCodes.Status500InternalServerError && !environment.IsDevelopment()
                ? "An internal server error has occurred. Please try again later."
                : exceptionMessage;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Detail = detail
            };

            if (exception is ValidationException { PropertyName: not null } validationException)
            {
                problemDetails.Extensions["errors"] = new Dictionary<string, string[]>
                {
                    [validationException.PropertyName] = [validationException.Message]
                };
            }

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problemDetails
            });
        }

        private static int MapStatusCode(Exception ex)
        => ex switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            NoAvailableSeatsException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
