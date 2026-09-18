using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventsService.Infrastructure.BackgroundServices
{
    public class BookingProcessingBackgroundService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository,
        ILogger<BookingProcessingBackgroundService> logger) : BackgroundService
    {
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan ProcessingDelay = TimeSpan.FromSeconds(2);

        private readonly SemaphoreSlim _processingSemaphore = new(1, 1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var bookings = await bookingRepository.GetBookingsAsync(stoppingToken);
                    var pendingBookings = bookings.Where(b => b.Status == BookingStatus.Pending).ToList();

                    var tasks = pendingBookings.Select(booking => ProcessBookingAsync(booking, stoppingToken));
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

        private async Task ProcessBookingAsync(Booking booking, CancellationToken stoppingToken)
        {
            logger.LogInformation("Processing booking {BookingId}", booking.Id);

            Event? bookedEvent = null;

            try
            {
                await Task.Delay(ProcessingDelay, stoppingToken);

                await _processingSemaphore.WaitAsync(stoppingToken);
                try
                {
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
                finally
                {
                    _processingSemaphore.Release();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while processing booking {BookingId}, rejecting", booking.Id);

                await _processingSemaphore.WaitAsync(stoppingToken);
                try
                {
                    booking.Reject(DateTime.UtcNow);
                    await bookingRepository.UpdateBookingAsync(booking, stoppingToken);

                    bookedEvent ??= await eventRepository.GetEventAsync(booking.EventId, stoppingToken);
                    if (bookedEvent is not null)
                    {
                        bookedEvent.ReleaseSeats();
                        await eventRepository.UpdateEventAsync(bookedEvent, stoppingToken);
                    }
                }
                finally
                {
                    _processingSemaphore.Release();
                }
            }
        }
    }
}
