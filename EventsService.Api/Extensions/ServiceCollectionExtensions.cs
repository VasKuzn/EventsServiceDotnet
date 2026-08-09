using EventsService.Application.Interfaces;
using EventsService.Application.Services;
using EventsService.Domain.Models;
using EventsService.Infrastructure.Repositories;

namespace EventsService.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IEventService, InMemoryEventService>();
            return services;
        }

        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddSingleton<List<Event>>(); //протестить
            services.AddSingleton<IEventRepository, InMemoryEventRepository>();
            return services;
        }
    }
}