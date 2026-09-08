using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventsService.Infrastructure.BackgroundServices
{
    public class BookingProcessingBackgroundService(
        IBookingRepository bookingRepository,
        ILogger<BookingProcessingBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var bookings = await bookingRepository.GetBookingsAsync(stoppingToken);
                    var pending = bookings.Where(b => b.Status == BookingStatus.Pending);

                    foreach (var booking in pending)
                    {
                        logger.LogInformation("Processing booking {BookingId}", booking.Id);

                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

                            booking.Confirm(DateTime.UtcNow);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Booking {BookingId} processing failed, rejecting", booking.Id);
                            booking.Reject(DateTime.UtcNow);
                        }

                        await bookingRepository.UpdateBookingAsync(booking, stoppingToken);

                        logger.LogInformation(
                            "Booking {BookingId} processed with status {Status}",
                            booking.Id,
                            booking.Status);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while processing pending bookings");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
