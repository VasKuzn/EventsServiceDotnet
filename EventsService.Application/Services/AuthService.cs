using EventsService.Application.DataTransferObjects.Auth;
using EventsService.Application.Interfaces.Auth;
using EventsService.Domain.Models;
using EventsService.Domain.SystemExceptions;

namespace EventsService.Application.Services
{
    public sealed class AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, IJwtTokenGenerator tokenGenerator) : IAuthService
    {
        public async Task RegisterAsync(RegisterUserDto credentials, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(credentials.Password))
                throw new ValidationException("Password is required.", nameof(credentials.Password));

            var user = User.Create(credentials.Login, passwordHasher.Hash(credentials.Password), credentials.Role);
            if (!await userRepository.TryCreateAsync(user, ct))
                throw new ValidationException("Login is already in use.", nameof(credentials.Login));
        }

        public async Task<TokenResponseDto> LoginAsync(LoginDto credentials, CancellationToken ct)
        {
            var user = await userRepository.FindByLoginAsync(credentials.Login, ct);
            if (user is null || !passwordHasher.Verify(credentials.Password, user.PasswordHash))
                throw new ValidationException("Invalid login or password.");

            return new TokenResponseDto(tokenGenerator.Generate(user));
        }
    }
}
