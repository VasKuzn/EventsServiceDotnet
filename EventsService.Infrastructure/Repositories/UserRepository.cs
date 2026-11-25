using EventsService.Application.Interfaces.Auth;
using EventsService.Domain.Models;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventsService.Infrastructure.Repositories
{
    public sealed class UserRepository(AppDbContext dbContext) : IUserRepository
    {
        public Task<User?> FindByLoginAsync(string login, CancellationToken ct)
        {
            return dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Login == login, ct);
        }

        public async Task<bool> TryCreateAsync(User user, CancellationToken ct)
        {
            var entry = dbContext.Users.Add(user);
            try
            {
                await dbContext.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_users_login" })
            {
                entry.State = EntityState.Detached;
                return false;
            }
        }
    }
}
