using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configurations;

public class FriendshipConfiguration : IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.ToTable("friendships");

        // Restrict on both sides: two cascade paths into the same table are not
        // allowed, and friendships are cleaned up explicitly on account deletion.
        builder.HasOne(f => f.Requester)
            .WithMany(u => u.SentFriendRequests)
            .HasForeignKey(f => f.RequesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Addressee)
            .WithMany(u => u.ReceivedFriendRequests)
            .HasForeignKey(f => f.AddresseeId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per ordered pair; the service rejects the reverse duplicate.
        builder.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();
        // Hot paths: "my friends" and "requests waiting on me".
        builder.HasIndex(f => new { f.AddresseeId, f.Status });
        builder.HasIndex(f => new { f.RequesterId, f.Status });
    }
}
