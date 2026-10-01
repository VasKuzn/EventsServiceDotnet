using EventsService.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsService.Infrastructure.DataAccess.Configurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("Booking");
            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id).IsRequired().ValueGeneratedNever();
            builder.Property(b => b.EventId).IsRequired();
            builder.Property(b => b.CreatedAt).IsRequired();
            builder.Property(b => b.Status).IsRequired().HasConversion<string>().HasMaxLength(20);


            builder.HasOne(b => b.Event).WithMany(e => e.Bookings).HasForeignKey(b => b.EventId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}