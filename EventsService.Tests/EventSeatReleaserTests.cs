using EventsService.Application.Interfaces.Events;
using EventsService.Domain.Models;
using EventsService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EventsService.Tests;

public class EventSeatReleaserTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ReleaseSeatsAsync_ConcurrencyConflicts_ReloadsEventAndReturnsOneSeat(int conflicts)
    {
        var eventId = Guid.NewGuid();
        var loadedEvents = new List<Event>();
        var resolvedRepositories = 0;
        var services = new ServiceCollection();
        services.AddScoped<IEventRepository>(_ =>
        {
            var attempt = ++resolvedRepositories;
            var eventEntity = Event.Create(eventId, "Conference", null,
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 10);
            Assert.True(eventEntity.TryReserveSeats());
            loadedEvents.Add(eventEntity);

            var repository = new Mock<IEventRepository>(MockBehavior.Strict);
            repository.Setup(r => r.GetEventAsync(eventId, CancellationToken.None))
                .ReturnsAsync(eventEntity);
            var update = repository.Setup(r => r.UpdateEventAsync(eventEntity, CancellationToken.None));
            if (attempt <= conflicts)
                update.ThrowsAsync(new DbUpdateConcurrencyException());
            else
                update.ReturnsAsync(eventEntity);
            return repository.Object;
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var releaser = new EventSeatReleaser(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EventSeatReleaser>.Instance);

        await releaser.ReleaseSeatsAsync(eventId, CancellationToken.None);

        Assert.Equal(conflicts + 1, resolvedRepositories);
        Assert.All(loadedEvents, eventEntity => Assert.Equal(10, eventEntity.AvailableSeats));
    }

    [Fact]
    public async Task ReleaseSeatsAsync_ConflictsPersist_StopsAfterThreeAttempts()
    {
        var eventId = Guid.NewGuid();
        var attempts = 0;
        var services = new ServiceCollection();
        services.AddScoped<IEventRepository>(_ =>
        {
            attempts++;
            var eventEntity = Event.Create(eventId, "Conference", null,
                DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 10);
            Assert.True(eventEntity.TryReserveSeats());
            var repository = new Mock<IEventRepository>(MockBehavior.Strict);
            repository.Setup(r => r.GetEventAsync(eventId, CancellationToken.None))
                .ReturnsAsync(eventEntity);
            repository.Setup(r => r.UpdateEventAsync(eventEntity, CancellationToken.None))
                .ThrowsAsync(new DbUpdateConcurrencyException());
            return repository.Object;
        });
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var releaser = new EventSeatReleaser(provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<EventSeatReleaser>.Instance);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            releaser.ReleaseSeatsAsync(eventId, CancellationToken.None));

        Assert.Equal(3, attempts);
    }
}
