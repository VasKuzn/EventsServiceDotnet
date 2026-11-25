using EventsService.Application.DataTransferObjects.Bookings;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventsService.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("bookings")]
    public sealed class BookingsController(IBookingService bookingService) : ControllerBase
    {
        private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

        [HttpGet("{bookingId}")]
        [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBookingById(Guid bookingId, CancellationToken cancellationToken)
        {
            var booking = await bookingService.GetBookingByIdAsync(bookingId, CurrentUserId, cancellationToken);
            return Ok(booking);
        }
        [HttpPost("/events/{eventId}/book")]
        [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateBooking(Guid eventId, CancellationToken cancellationToken)
        {
            var booking = await bookingService.CreateBookingAsync(eventId, CurrentUserId, cancellationToken);

            return AcceptedAtAction(
                nameof(GetBookingById),
                new { bookingId = booking.Id }, booking);
        }

        [HttpDelete("{bookingId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CancelBooking(Guid bookingId, CancellationToken cancellationToken)
        {
            var role = User.IsInRole(nameof(UserRole.Admin)) ? UserRole.Admin : UserRole.User;
            await bookingService.CancelBookingAsync(bookingId, CurrentUserId, role, cancellationToken);
            return NoContent();
        }
    }
}
