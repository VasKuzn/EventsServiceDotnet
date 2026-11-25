using EventsService.Application.DataTransferObjects.Auth;

namespace EventsService.Application.Interfaces.Auth
{
    public interface IAuthService
    {
        public Task RegisterAsync(RegisterUserDto credentials, CancellationToken ct);
        public Task<TokenResponseDto> LoginAsync(LoginDto credentials, CancellationToken ct);
    }
}
