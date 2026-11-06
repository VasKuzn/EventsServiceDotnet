using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Application.Extensions;
using EventsService.Infrastructure.DataAccess;
using EventsService.Infrastructure.Repositories;
using EventsService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventsService.Tests;

/// <summary>
/// DI-контейнер для тестов: AppDbContext на InMemory-провайдере с уникальной базой на каждый экземпляр.
/// </summary>
public sealed class TestServiceProvider : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public TestServiceProvider()
    {
        // Имя базы вынесено в переменную: все scope одного контейнера работают с одной и той же базой.
        var dbName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IEventSeatReleaser, EventSeatReleaser>();
        services.AddApplicationServices();

        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    public IServiceScope CreateScope() => _serviceProvider.CreateScope();

    public void Dispose() => _serviceProvider.Dispose();
}
