using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Application.Services
{
    public sealed class InMemoryBookingService(IBookingRepository bookingRepository, IEventRepository eventRepository) : IBookingService
    {
        private readonly object _bookingLock = new();

        public Task<BookingResponseDto> CreateBookingAsync(Guid eventId, CancellationToken ct)
        {
            lock (_bookingLock)
            {
                var eventEntity = eventRepository.GetEventAsync(eventId, ct).GetAwaiter().GetResult()
                    ?? throw new NotFoundException($"Event with ID {eventId} not found.");

                if (!eventEntity.TryReserveSeats())
                {
                    throw new NoAvailableSeatsException();
                }

                eventRepository.UpdateEventAsync(eventEntity, ct).GetAwaiter().GetResult();

                var response = bookingRepository.CreateBookingAsync(Booking.Create(eventId), ct).GetAwaiter().GetResult();

                return Task.FromResult(BookingResponseDto.FromEntity(response));
            }
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