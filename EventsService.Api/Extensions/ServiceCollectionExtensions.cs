using EventsService.Application.Interfaces.Events;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Services;
using EventsService.Domain.Models;
using EventsService.Infrastructure.BackgroundServices;
using EventsService.Infrastructure.Repositories;

namespace EventsService.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IEventService, InMemoryEventService>();
            services.AddSingleton<IBookingService, InMemoryBookingService>();
            return services;
        }

        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddSingleton<List<Event>>();
            services.AddSingleton<List<Booking>>();
            services.AddSingleton<IEventRepository, InMemoryEventRepository>();
            services.AddSingleton<IBookingRepository, InMemoryBookingRepository>();
            services.AddHostedService<BookingProcessingBackgroundService>();
            return services;
        }
    }
}