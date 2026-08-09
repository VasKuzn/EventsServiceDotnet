using System.ComponentModel.DataAnnotations;

namespace EventsService.Application.DataTransferObjects
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

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndAt <= StartAt)
            {
                yield return new ValidationResult(
                    "EndAt должен быть позже StartAt",
                    new[] { nameof(EndAt) });
            }
        }
    }
}