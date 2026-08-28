using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configurations;

public class HabitGroupConfiguration : IEntityTypeConfiguration<HabitGroup>
{
    public void Configure(EntityTypeBuilder<HabitGroup> builder)
    {
        builder.ToTable("habit_groups");
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(500);
        builder.Property(g => g.Color).HasMaxLength(7).IsRequired();
        builder.Property(g => g.Icon).HasMaxLength(16).IsRequired();

        // Groups are removed explicitly when their owner's account is deleted.
        builder.HasOne(g => g.Owner)
            .WithMany()
            .HasForeignKey(g => g.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => g.OwnerId);
    }
}

public class HabitGroupMemberConfiguration : IEntityTypeConfiguration<HabitGroupMember>
{
    public void Configure(EntityTypeBuilder<HabitGroupMember> builder)
    {
        builder.ToTable("habit_group_members");

        builder.HasOne(m => m.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Habits are archived rather than deleted, so a link never dangles.
        builder.HasOne(m => m.Habit)
            .WithMany()
            .HasForeignKey(m => m.HabitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
        builder.HasIndex(m => new { m.UserId, m.Status });
    }
}
