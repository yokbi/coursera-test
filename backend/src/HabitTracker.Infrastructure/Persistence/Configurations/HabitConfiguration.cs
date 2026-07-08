using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configurations;

public class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.ToTable("habits");
        builder.Property(h => h.Name).HasMaxLength(100).IsRequired();
        builder.Property(h => h.Description).HasMaxLength(500);
        builder.Property(h => h.Color).HasMaxLength(7).IsRequired();
        builder.Property(h => h.Icon).HasMaxLength(16).IsRequired();
        builder.Property(h => h.Category).HasMaxLength(50);
        builder.Property(h => h.Unit).HasMaxLength(30);
        builder.Property(h => h.TargetValue).HasPrecision(10, 2);

        builder.HasOne(h => h.User)
            .WithMany(u => u.Habits)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => new { h.UserId, h.SortOrder });
        builder.HasIndex(h => new { h.UserId, h.IsArchived });
    }
}
