using EventsService.Application.DataTransferObjects.Auth;
using EventsService.Application.Interfaces.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventsService.Api.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("auth")]
    public sealed class AuthController(IAuthService authService) : ControllerBase
    {
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register(RegisterUserDto credentials, CancellationToken ct)
        {
            await authService.RegisterAsync(credentials, ct);
            return NoContent();
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Login(LoginDto credentials, CancellationToken ct)
        {
            return Ok(await authService.LoginAsync(credentials, ct));
        }
    }
}
