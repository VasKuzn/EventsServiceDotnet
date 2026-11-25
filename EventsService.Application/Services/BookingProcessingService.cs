using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using Microsoft.Extensions.Logging;

namespace EventsService.Application.Services
{
    public sealed class BookingProcessingService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository,
        IEventSeatReleaser eventSeatReleaser,
        ILogger<BookingProcessingService> logger) : IBookingProcessingService
    {
        public Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken ct) =>
            bookingRepository.GetPendingBookingIdsAsync(ct);

        public async Task ProcessBookingAsync(Guid bookingId, CancellationToken ct)
        {
            logger.LogInformation("Processing booking {BookingId}", bookingId);

            Booking? booking = null;

            try
            {
                booking = await bookingRepository.GetBookingAsync(bookingId, ct);
                if (booking is null || booking.Status != BookingStatus.Pending)
                {
                    logger.LogWarning("Booking {BookingId} not found or already processed, skipping", bookingId);
                    return;
                }

                var bookedEvent = await eventRepository.GetEventAsync(booking.EventId, ct);
                if (bookedEvent is null)
                {
                    booking.Reject(DateTime.UtcNow);
                    await bookingRepository.UpdateBookingAsync(booking, ct);

                    logger.LogWarning(
                        "Event {EventId} for booking {BookingId} not found, booking rejected",
                        booking.EventId,
                        booking.Id);
                    return;
                }

                booking.Confirm(DateTime.UtcNow);
                await bookingRepository.UpdateBookingAsync(booking, ct);

                logger.LogInformation(
                    "Booking {BookingId} processed with status {Status}",
                    booking.Id,
                    booking.Status);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while processing booking {BookingId}", bookingId);

                if (booking is null)
                {
                    return;
                }

                if (booking.Status != BookingStatus.Pending)
                {
                    throw;
                }

                booking.Reject(DateTime.UtcNow);
                await bookingRepository.UpdateBookingAsync(booking, ct);
                await eventSeatReleaser.ReleaseSeatsAsync(booking.EventId, ct);
            }
        }
    }
}
