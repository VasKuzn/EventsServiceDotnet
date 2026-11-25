using EventsService.Domain.Models;

namespace EventsService.Application.Interfaces.Auth
{
    public interface IJwtTokenGenerator
    {
        public string Generate(User user);
    }
}
