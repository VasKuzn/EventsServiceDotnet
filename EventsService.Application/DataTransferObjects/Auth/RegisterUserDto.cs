using System.ComponentModel.DataAnnotations;
using EventsService.Domain.Enums;
using EventsService.Domain.Models;

namespace EventsService.Application.DataTransferObjects.Auth
{
    public sealed record RegisterUserDto
    {
        [Required, MaxLength(User.LoginMaxLength)]
        public required string Login { get; init; }

        [Required]
        public required string Password { get; init; }

        public UserRole Role { get; init; } = UserRole.User;
    }
}
