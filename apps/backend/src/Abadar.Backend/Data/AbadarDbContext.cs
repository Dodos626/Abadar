using Abadar.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Data;

public sealed class AbadarDbContext(DbContextOptions<AbadarDbContext> options)
    : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<AppUser>();

        user.ToTable("users");
        user.HasKey(value => value.Id);
        user.Property(value => value.Id).HasColumnName("id");
        user.Property(value => value.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        user.Property(value => value.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        user.Property(value => value.LastOnline).HasColumnName("last_online");
        user.Property(value => value.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        user.Property(value => value.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        user.Property(value => value.Username).HasColumnName("username").HasMaxLength(50).IsRequired();
        user.Property(value => value.NormalizedUsername).HasColumnName("normalized_username").HasMaxLength(50).IsRequired();
        user.Property(value => value.PasswordHash).HasColumnName("password_hash").IsRequired();
        user.Property(value => value.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        user.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        user.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();

        user.HasIndex(value => value.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_email");
        user.HasIndex(value => value.NormalizedUsername)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_username");
    }
}