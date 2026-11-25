using EventsService.Application.Interfaces.Auth;
using EventsService.Application.Interfaces.Bookings;
using EventsService.Application.Interfaces.Events;
using EventsService.Infrastructure.BackgroundServices;
using EventsService.Infrastructure.DataAccess;
using EventsService.Infrastructure.Repositories;
using EventsService.Infrastructure.Security;
using EventsService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventsService.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
            services.AddInfrastructureAdapters();
            services.AddHostedService<BookingProcessingBackgroundService>();
            return services;
        }

        public static IServiceCollection AddInfrastructureAdapters(this IServiceCollection services)
        {
            services.AddOptions();
            services.AddScoped<IEventRepository, EventRepository>();
            services.AddScoped<IBookingRepository, BookingRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IEventSeatReleaser, EventSeatReleaser>();
            services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
            services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            return services;
        }
    }
}
