using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Application.Services;
using EventsService.Infrastructure.DataAccess;
using EventsService.Infrastructure.Repositories;
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
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        _serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    public IServiceScope CreateScope() => _serviceProvider.CreateScope();

    public void Dispose() => _serviceProvider.Dispose();
}
