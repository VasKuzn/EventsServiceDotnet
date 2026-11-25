using EventsService.Domain.Models;

namespace EventsService.Application.Interfaces.Auth
{
    public interface IUserRepository
    {
        public Task<User?> FindByLoginAsync(string login, CancellationToken ct);
        public Task<bool> TryCreateAsync(User user, CancellationToken ct);
    }
}
