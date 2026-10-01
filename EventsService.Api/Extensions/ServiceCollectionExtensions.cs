using EventsService.Application.Interfaces.Events;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Services;
using EventsService.Domain.Models;
using EventsService.Infrastructure.BackgroundServices;
using EventsService.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;
using EventsService.Infrastructure.Repositories;

namespace EventsService.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IBookingService, BookingService>();
            return services;
        }

        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddHostedService<BookingProcessingBackgroundService>();
            return services;
        }
    }
}