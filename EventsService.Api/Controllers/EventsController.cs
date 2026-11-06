using EventsService.Application.DataTransferObjects.Events;
using EventsService.Application.Interfaces.Events;
using Microsoft.AspNetCore.Mvc;

namespace EventsService.Api.Controllers
{
    [ApiController]
    [Route("events")]
    public sealed class EventsController(IEventService eventService) : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType(typeof(PaginatedResult<EventResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllEvents(string? title = null, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            var events = await eventService.GetEventsAsync(title, from, to, page, pageSize, cancellationToken);
            return Ok(events);
        }
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetEventById(Guid id, CancellationToken cancellationToken)
        {
            var eventToFind = await eventService.GetEventAsync(id, cancellationToken);
            return Ok(eventToFind);
        }
        [HttpPost]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto newEvent, CancellationToken cancellationToken)
        {
            var createdEvent = await eventService.CreateEventAsync(newEvent, cancellationToken);
            return CreatedAtAction(nameof(GetEventById), new { id = createdEvent.Id }, createdEvent);
        }
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventDto updatedEvent, CancellationToken cancellationToken)
        {
            var eventToUpdate = await eventService.UpdateEventAsync(id, updatedEvent, cancellationToken);
            return Ok(eventToUpdate);
        }
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken cancellationToken)
        {
            await eventService.DeleteEventAsync(id, cancellationToken);
            return NoContent();
        }
    }
}
