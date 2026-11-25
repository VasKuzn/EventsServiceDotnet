using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Application.Services
{
    public sealed class BookingService(IBookingRepository bookingRepository) : IBookingService
    {
        private const int ActiveBookingLimit = 10;

        public async Task<BookingResponseDto> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken ct)
        {
            var booking = await bookingRepository.CreateBookingWithReservedSeatAsync(eventId, userId, ValidateBooking, ct);
            return BookingResponseDto.FromEntity(booking);
        }

        public async Task<BookingResponseDto> GetBookingByIdAsync(Guid bookingId, Guid userId, CancellationToken ct)
        {
            if (userId == Guid.Empty)
                throw new ForbiddenException();

            var booking = await bookingRepository.GetBookingAsync(bookingId, ct);

            return booking is null
                ? throw new NotFoundException($"Booking with ID {bookingId} not found.")
                : BookingResponseDto.FromEntity(booking);
        }

        public Task CancelBookingAsync(Guid bookingId, Guid userId, UserRole role, CancellationToken ct)
        {
            return bookingRepository.CancelBookingAndReleaseSeatAsync(bookingId, booking =>
            {
                if (booking.UserId != userId && role != UserRole.Admin)
                    throw new ForbiddenException();

                if (booking.Status == BookingStatus.Cancelled)
                    return false;

                booking.Cancel(DateTime.UtcNow);
                return true;
            }, ct);
        }

        private static void ValidateBooking(Event bookedEvent, int activeBookingCount)
        {
            if (bookedEvent.StartAt <= DateTime.UtcNow)
                throw new EventAlreadyStartedException();

            if (activeBookingCount >= ActiveBookingLimit)
                throw new ActiveBookingLimitExceededException(ActiveBookingLimit);
        }
    }
}
