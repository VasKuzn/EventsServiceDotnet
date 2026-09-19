using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace EventsService.Api.Controllers
{
    [ApiController]
    [Route("bookings")]
    public sealed class BookingsController(IBookingService bookingService) : ControllerBase
    {
        [HttpGet("{bookingId}")]
        [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBookingById(Guid bookingId, CancellationToken cancellationToken)
        {
            var eventToFind = await bookingService.GetBookingByIdAsync(bookingId, cancellationToken);
            return Ok(eventToFind);
        }
        [HttpPost("/events/{eventId}/book")]
        [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateBooking(Guid eventId, CancellationToken cancellationToken)
        {
            var booking = await bookingService.CreateBookingAsync(eventId, cancellationToken);

            return AcceptedAtAction(
                nameof(GetBookingById),
                new { bookingId = booking.Id }, booking);
        }
    }
}