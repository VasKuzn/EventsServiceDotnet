using EventsService.Application.Interfaces.Bookings;
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

        private async Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken stoppingToken)
        {
            using var scope = scopeFactory.CreateScope();
            var bookingProcessingService = scope.ServiceProvider.GetRequiredService<IBookingProcessingService>();

            return await bookingProcessingService.GetPendingBookingIdsAsync(stoppingToken);
        }

        private async Task ProcessBookingAsync(Guid bookingId, CancellationToken stoppingToken)
        {
            await Task.Delay(ProcessingDelay, stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var bookingProcessingService = scope.ServiceProvider.GetRequiredService<IBookingProcessingService>();
            await bookingProcessingService.ProcessBookingAsync(bookingId, stoppingToken);
        }
    }
}
