using EventsService.Application.Interfaces.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EventsService.Infrastructure.Services
{
    public sealed class EventSeatReleaser(
        IServiceScopeFactory scopeFactory,
        ILogger<EventSeatReleaser> logger) : IEventSeatReleaser
    {
        private const int MaxConcurrencyAttempts = 3;

        public async Task ReleaseSeatsAsync(Guid eventId, CancellationToken ct)
        {
            for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
            {
                using var scope = scopeFactory.CreateScope();
                var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

                var bookedEvent = await eventRepository.GetEventAsync(eventId, ct);
                if (bookedEvent is null)
                {
                    return;
                }

                try
                {
                    bookedEvent.ReleaseSeats();
                    await eventRepository.UpdateEventAsync(bookedEvent, ct);
                    return;
                }
                catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyAttempts)
                {
                    logger.LogWarning(
                        "Concurrency conflict while releasing seats of event {EventId}, attempt {Attempt}",
                        eventId,
                        attempt);
                }
            }
        }
    }
}
