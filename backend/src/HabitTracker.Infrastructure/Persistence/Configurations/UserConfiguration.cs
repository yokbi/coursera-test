using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.Property(u => u.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();
        // Nullable: accounts provisioned through Google may never set a password.
        builder.Property(u => u.PasswordHash).HasMaxLength(500);
        builder.Property(u => u.GoogleSubject).HasMaxLength(255);
        // One Google account maps to at most one user; nulls are exempt in Postgres.
        builder.HasIndex(u => u.GoogleSubject).IsUnique();
        builder.Property(u => u.TimeZone).HasMaxLength(100).IsRequired();
    }
}
