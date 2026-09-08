using EventsService.Application.Interfaces.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace EventsService.Api.Controllers
{
    [ApiController]
    [Route("bookings")]
    public sealed class BookingsController(IBookingService bookingService) : ControllerBase
    {
        [HttpGet("{bookingId}")]
        public async Task<IActionResult> GetBookingById(Guid bookingId, CancellationToken cancellationToken)
        {
            var eventToFind = await bookingService.GetBookingByIdAsync(bookingId, cancellationToken);
            return Ok(eventToFind);
        }
        [HttpPost("/events/{eventId}/book")]
        public async Task<IActionResult> CreateBooking(Guid eventId, CancellationToken cancellationToken)
        {
            var booking = await bookingService.CreateBookingAsync(eventId, cancellationToken);
            if (booking is null)
                return NotFound();

            return AcceptedAtAction(
                nameof(GetBookingById),
                new { bookingId = booking.Id }, booking);
        }
    }
}