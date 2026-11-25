using EventsService.Domain.Enums;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Domain.Models
{
    public sealed class User
    {
        public const int LoginMaxLength = 100;

        public Guid Id { get; private set; }

        public string Login { get; private set; }

        public string PasswordHash { get; private set; }

        public UserRole Role { get; private set; }

        private User(Guid id, string login, string passwordHash, UserRole role)
        {
            Id = id;
            Login = login;
            PasswordHash = passwordHash;
            Role = role;
        }

        public static User Create(string login, string passwordHash, UserRole role = UserRole.User)
        {
            if (string.IsNullOrWhiteSpace(login) || login.Length > LoginMaxLength)
                throw new ValidationException($"Login must contain between 1 and {LoginMaxLength} characters.", nameof(login));

            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new ValidationException("Password hash is required.", nameof(passwordHash));

            if (!Enum.IsDefined(role))
                throw new ValidationException("Invalid user role.", nameof(role));

            return new User(Guid.NewGuid(), login, passwordHash, role);
        }
    }
}
