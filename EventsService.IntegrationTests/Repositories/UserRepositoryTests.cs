using EventsService.Domain.Models;
using EventsService.Infrastructure.Repositories;
using EventsService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EventsService.IntegrationTests.Repositories;

public sealed class UserRepositoryTests(PostgresFixture fixture) : DatabaseTestBase(fixture)
{
    [Fact]
    public async Task TryCreateAsync_ConcurrentDuplicateLogin_StoresOnlyOneUser()
    {
        var first = User.Create("same-login", "first-hash");
        var second = User.Create(first.Login, "second-hash");
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();

        var results = await Task.WhenAll(
            new UserRepository(firstContext).TryCreateAsync(first, CancellationToken.None),
            new UserRepository(secondContext).TryCreateAsync(second, CancellationToken.None));

        Assert.Single(results, created => created);
        var expected = results[0] ? first : second;
        await using var readContext = CreateContext();
        var stored = await new UserRepository(readContext).FindByLoginAsync(first.Login, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(expected.Id, stored.Id);
        Assert.Equal(expected.PasswordHash, stored.PasswordHash);
        Assert.Equal(1, await readContext.Users.CountAsync(user => user.Login == first.Login));
    }
}
