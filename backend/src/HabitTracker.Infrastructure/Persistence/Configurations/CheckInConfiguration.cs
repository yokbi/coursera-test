using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configurations;

public class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("check_ins");
        builder.Property(c => c.Value).HasPrecision(10, 2);

        builder.HasOne(c => c.Habit)
            .WithMany(h => h.CheckIns)
            .HasForeignKey(c => c.HabitId)
            .OnDelete(DeleteBehavior.Cascade);

        // One check-in row per habit per local day; updates mutate the row.
        builder.HasIndex(c => new { c.HabitId, c.Date }).IsUnique();
        // Hot path: "all of this user's check-ins for day/range" (dashboard, weekly grid).
        builder.HasIndex(c => new { c.UserId, c.Date });
    }
}
