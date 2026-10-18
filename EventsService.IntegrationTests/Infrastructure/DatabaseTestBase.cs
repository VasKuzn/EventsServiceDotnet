using EventsService.Infrastructure.DataAccess;

namespace EventsService.IntegrationTests.Infrastructure;

[Collection(PostgresCollection.Name)]
public abstract class DatabaseTestBase(PostgresFixture fixture) : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; } = fixture;

    public Task InitializeAsync() => Fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected AppDbContext CreateContext() => Fixture.CreateContext();

    protected async Task SeedAsync(params object[] entities)
    {
        await using var context = CreateContext();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }
}
