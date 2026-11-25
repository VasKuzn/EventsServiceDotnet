using EventsService.Domain.Enums;
using EventsService.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsService.Infrastructure.DataAccess.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users");
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever().IsRequired();
            builder.Property(u => u.Login).HasColumnName("login").HasMaxLength(User.LoginMaxLength).IsRequired();
            builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
            builder.Property(u => u.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();

            builder.HasIndex(u => u.Login).IsUnique();
            builder.HasData(new
            {
                Id = Booking.LegacyOwnerId,
                Login = "__legacy_bookings__",
                PasswordHash = "!",
                Role = UserRole.User
            });
        }
    }
}
