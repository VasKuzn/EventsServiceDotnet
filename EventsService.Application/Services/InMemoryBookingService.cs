using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Application.Services
{
    public sealed class InMemoryBookingService(IBookingRepository bookingRepository, IEventRepository eventRepository) : IBookingService
    {
        private readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

        public async Task<BookingResponseDto> CreateBookingAsync(Guid eventId, CancellationToken ct)
        {
            await _bookingSemaphore.WaitAsync(ct);
            try
            {
                var eventEntity = await eventRepository.GetEventAsync(eventId, ct)
                    ?? throw new NotFoundException($"Event with ID {eventId} not found.");

                if (!eventEntity.TryReserveSeats())
                {
                    throw new NoAvailableSeatsException();
                }

                await eventRepository.UpdateEventAsync(eventEntity, ct);

                var response = await bookingRepository.CreateBookingAsync(Booking.Create(eventId), ct);

                return BookingResponseDto.FromEntity(response);
            }
            finally
            {
                _bookingSemaphore.Release();
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