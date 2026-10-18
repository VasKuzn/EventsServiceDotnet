using EventsService.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsService.Infrastructure.DataAccess.Configurations
{
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("bookings");
            builder.HasKey(b => b.Id);

            builder.Property(b => b.Id).HasColumnName("id").IsRequired().ValueGeneratedNever();
            builder.Property(b => b.EventId).HasColumnName("event_id").IsRequired();
            builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(b => b.ProcessedAt).HasColumnName("processed_at");
            builder.Property(b => b.Status).HasColumnName("status").IsRequired().HasConversion<string>().HasMaxLength(20);


            builder.HasOne(b => b.Event).WithMany(e => e.Bookings).HasForeignKey(b => b.EventId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
