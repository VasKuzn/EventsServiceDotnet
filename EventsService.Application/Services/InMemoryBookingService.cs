using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Application.Services
{
    public sealed class InMemoryBookingService(IBookingRepository bookingRepository, IEventRepository eventRepository) : IBookingService
    {
        public async Task<BookingResponseDto> CreateBookingAsync(Guid eventId, CancellationToken ct)
        {
            _ = await eventRepository.GetEventAsync(eventId, ct)
            ?? throw new NotFoundException($"Event with ID {eventId} not found.");

            var response = await bookingRepository.CreateBookingAsync(Booking.Create(eventId), ct);

            return BookingResponseDto.FromEntity(response);
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