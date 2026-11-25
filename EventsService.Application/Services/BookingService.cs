using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Application.Services
{
    public sealed class BookingService(IBookingRepository bookingRepository) : IBookingService
    {
        public async Task<BookingResponseDto> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken ct)
        {
            var booking = await bookingRepository.CreateBookingWithReservedSeatAsync(eventId, userId, ct);
            return BookingResponseDto.FromEntity(booking);
        }

        public async Task<BookingResponseDto> GetBookingByIdAsync(Guid bookingId, CancellationToken ct)
        {
            var booking = await bookingRepository.GetBookingAsync(bookingId, ct);

            return booking is null
                ? throw new NotFoundException($"Booking with ID {bookingId} not found.")
                : BookingResponseDto.FromEntity(booking);
        }
    }
}
