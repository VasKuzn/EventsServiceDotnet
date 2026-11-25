using System.ComponentModel.DataAnnotations;

namespace EventsService.Application.DataTransferObjects.Auth
{
    public sealed record LoginDto
    {
        [Required]
        public required string Login { get; init; }

        [Required]
        public required string Password { get; init; }
    }
}
