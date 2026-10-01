using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventsService.Infrastructure.BackgroundServices
{
    public class BookingProcessingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingProcessingBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);
        private const int MaxConcurrencyAttempts = 3;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var pendingBookingIds = await GetPendingBookingIdsAsync(stoppingToken);

                    var tasks = pendingBookingIds.Select(id => ProcessBookingAsync(id, stoppingToken));
                    await Task.WhenAll(tasks);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while processing pending bookings");
                }

                await Task.Delay(PollingInterval, stoppingToken);
            }
        }

        private async Task<List<Guid>> GetPendingBookingIdsAsync(CancellationToken stoppingToken)
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            return await dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.Status == BookingStatus.Pending)
                .Select(b => b.Id)
                .ToListAsync(stoppingToken);
        }

        private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
        {
            logger.LogInformation("Processing booking {BookingId}", bookingId);

            using var scope = scopeFactory.CreateScope();
            var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
            var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

            Booking? booking = null;
            Event? bookedEvent = null;

            try
            {
                await Task.Delay(ProcessingDelay, stoppingToken);

                booking = await bookingRepository.GetBookingAsync(bookingId, stoppingToken);
                if (booking is null || booking.Status != BookingStatus.Pending)
                {
                    logger.LogWarning("Booking {BookingId} not found or already processed, skipping", bookingId);
                    return;
                }

                bookedEvent = await eventRepository.GetEventAsync(booking.EventId, stoppingToken);
                if (bookedEvent is null)
                {
                    booking.Reject(DateTime.UtcNow);
                    await bookingRepository.UpdateBookingAsync(booking, stoppingToken);

                    logger.LogWarning(
                        "Event {EventId} for booking {BookingId} not found, booking rejected",
                        booking.EventId,
                        booking.Id);
                    return;
                }

                booking.Confirm(DateTime.UtcNow);
                await bookingRepository.UpdateBookingAsync(booking, stoppingToken);

                logger.LogInformation(
                    "Booking {BookingId} processed with status {Status}",
                    booking.Id,
                    booking.Status);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while processing booking {BookingId}, rejecting", bookingId);

                if (booking is null)
                {
                    return;
                }

                booking.Reject(DateTime.UtcNow);
                await bookingRepository.UpdateBookingAsync(booking, stoppingToken);

                await ReleaseSeatsAsync(booking.EventId, stoppingToken);
            }
        }

        private async Task ReleaseSeatsAsync(Guid eventId, CancellationToken stoppingToken)
        {
            for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
            {
                // Новый scope на каждую попытку: после конфликта контекст хранит устаревшую сущность.
                using var scope = scopeFactory.CreateScope();
                var eventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();

                var bookedEvent = await eventRepository.GetEventAsync(eventId, stoppingToken);
                if (bookedEvent is null)
                {
                    return;
                }

                try
                {
                    bookedEvent.ReleaseSeats();
                    await eventRepository.UpdateEventAsync(bookedEvent, stoppingToken);
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
