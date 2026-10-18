using EventsService.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventsService.Infrastructure.DataAccess.Configurations
{
    public class EventConfiguration : IEntityTypeConfiguration<Event>
    {
        public void Configure(EntityTypeBuilder<Event> builder)
        {
            builder.ToTable("events");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever().IsRequired();
            builder.Property(x => x.Title).HasColumnName("title").IsRequired().HasMaxLength(250);
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(2500);
            builder.Property(x => x.StartAt).HasColumnName("start_at").IsRequired();
            builder.Property(x => x.EndAt).HasColumnName("end_at").IsRequired();
            builder.Property(x => x.TotalSeats).HasColumnName("total_seats").IsRequired();
            builder.Property(x => x.AvailableSeats).HasColumnName("available_seats").IsRequired();

            builder.HasMany(e => e.Bookings).WithOne(x => x.Event).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);

        }
    }
}
