using EventsService.Application.DataTransferObjects;
using EventsService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EventsService.Api.Controllers
{
    [ApiController]
    [Route("events")]
    public sealed class EventsController(IEventService eventService) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAllEvents(CancellationToken cancellationToken)
        {
            var events = await eventService.GetEventsAsync(cancellationToken);
            return Ok(events);
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEventById(Guid id, CancellationToken cancellationToken)
        {
            var eventToFind = await eventService.GetEventAsync(id, cancellationToken);
            if (eventToFind == null)
            {
                return NotFound();
            }
            return Ok(eventToFind);
        }
        [HttpPost]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto newEvent, CancellationToken cancellationToken)
        {
            var createdEvent = await eventService.CreateEventAsync(newEvent, cancellationToken);
            return CreatedAtAction(nameof(GetEventById), new { id = createdEvent.Id }, createdEvent);
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] UpdateEventDto updatedEvent, CancellationToken cancellationToken)
        {
            var eventToUpdate = await eventService.UpdateEventAsync(id, updatedEvent, cancellationToken);
            if (eventToUpdate == null)
            {
                return NotFound();
            }
            return Ok(eventToUpdate);
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(Guid id, CancellationToken cancellationToken)
        {
            var deleted = await eventService.DeleteEventAsync(id, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}