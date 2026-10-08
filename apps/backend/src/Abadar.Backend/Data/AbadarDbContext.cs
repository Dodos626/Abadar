using Abadar.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Data;

// Defines durable user, order, and trade storage for the Version 1 backend.
public sealed class AbadarDbContext(DbContextOptions<AbadarDbContext> options)
    : DbContext(options)
{
    // Exposes persisted user accounts.
    public DbSet<AppUser> Users => Set<AppUser>();
    // Exposes durable order lifecycle records.
    public DbSet<ExchangeOrder> Orders => Set<ExchangeOrder>();
    // Exposes immutable trade history records.
    public DbSet<ExchangeTrade> Trades => Set<ExchangeTrade>();

    // Configures table names, financial precision, conversions, and recovery indexes.
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

        var order = modelBuilder.Entity<ExchangeOrder>();
        order.ToTable("orders");
        order.HasKey(value => value.Id);
        order.Property(value => value.Id).HasColumnName("id");
        order.Property(value => value.AccountId).HasColumnName("account_id");
        order.Property(value => value.Symbol).HasColumnName("symbol").HasMaxLength(30).IsRequired();
        order.Property(value => value.Side).HasColumnName("side").HasMaxLength(10).IsRequired();
        order.Property(value => value.Type).HasColumnName("type").HasMaxLength(10).IsRequired();
        order.Property(value => value.Price).HasColumnName("price").HasPrecision(30, 10);
        order.Property(value => value.Quantity).HasColumnName("quantity").HasPrecision(30, 10).IsRequired();
        order.Property(value => value.RemainingQuantity).HasColumnName("remaining_quantity").HasPrecision(30, 10).IsRequired();
        order.Property(value => value.Status).HasColumnName("status").HasMaxLength(30).IsRequired();
        order.Property(value => value.Sequence).HasColumnName("sequence").IsRequired();
        order.Property(value => value.CreatedAt).HasColumnName("created_at").IsRequired();
        order.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();
        order.HasIndex(value => new { value.Symbol, value.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_orders_symbol_sequence");
        order.HasIndex(value => new { value.Symbol, value.Status })
            .HasDatabaseName("ix_orders_symbol_status");

        var trade = modelBuilder.Entity<ExchangeTrade>();
        trade.ToTable("trades");
        trade.HasKey(value => value.Id);
        trade.Property(value => value.Id).HasColumnName("id");
        trade.Property(value => value.Symbol).HasColumnName("symbol").HasMaxLength(30).IsRequired();
        trade.Property(value => value.BuyOrderId).HasColumnName("buy_order_id");
        trade.Property(value => value.SellOrderId).HasColumnName("sell_order_id");
        trade.Property(value => value.Price).HasColumnName("price").HasPrecision(30, 10).IsRequired();
        trade.Property(value => value.Quantity).HasColumnName("quantity").HasPrecision(30, 10).IsRequired();
        trade.Property(value => value.Sequence).HasColumnName("sequence").IsRequired();
        trade.Property(value => value.ExecutedAt).HasColumnName("executed_at").IsRequired();
        trade.HasIndex(value => new { value.Symbol, value.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_trades_symbol_sequence");
        trade.HasIndex(value => value.ExecutedAt)
            .HasDatabaseName("ix_trades_executed_at");
    }
}