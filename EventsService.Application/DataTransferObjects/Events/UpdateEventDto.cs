using System.ComponentModel.DataAnnotations;
using EventsService.Domain.Models;

namespace EventsService.Application.DataTransferObjects.Events
{
    public record UpdateEventDto : IValidatableObject
    {
        [Required(ErrorMessage = "Значение заголовка обязательно для заполнения")]
        public required string Title { get; init; }

        public string? Description { get; init; }

        [Required(ErrorMessage = "Значение начального времени события обязательно для заполнения")]
        public required DateTime StartAt { get; init; }

        [Required(ErrorMessage = "Значение завершающего времени события обязательно для заполнения")]
        public required DateTime EndAt { get; init; }

        [Required(ErrorMessage = "Значение общего количества мест события обязательно для заполнения")]
        public int? TotalSeats { get; init; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndAt <= StartAt)
            {
                yield return new ValidationResult(
                    "EndAt должен быть позже StartAt",
                    new[] { nameof(EndAt) });
            }
            if (StartAt < DateTime.UtcNow)
            {
                yield return new ValidationResult(
                    "StartAt не может быть в прошлом",
                    new[] { nameof(StartAt) });
            }
            if (TotalSeats is <= 0)
            {
                yield return new ValidationResult(
                    "TotalSeats должен быть больше нуля",
                    new[] { nameof(TotalSeats) });
            }
        }

        public Event ToEntity(Guid id) => Event.Create(id, Title, Description, StartAt, EndAt, TotalSeats.GetValueOrDefault());
    }
}