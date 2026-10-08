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
    // Exposes durable events waiting for Kafka publication.
    public DbSet<IntegrationEventOutbox> OutboxEvents => Set<IntegrationEventOutbox>();
    // Exposes consumer idempotency markers.
    public DbSet<ProcessedIntegrationEvent> ProcessedEvents => Set<ProcessedIntegrationEvent>();
    // Exposes the portfolio consumer projection.
    public DbSet<PortfolioPosition> PortfolioPositions => Set<PortfolioPosition>();
    // Exposes the market-data consumer projection.
    public DbSet<MarketProjection> MarketProjections => Set<MarketProjection>();
    // Exposes the analytics consumer projection.
    public DbSet<AnalyticsProjection> AnalyticsProjections => Set<AnalyticsProjection>();

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

        var outbox = modelBuilder.Entity<IntegrationEventOutbox>();
        outbox.ToTable("integration_event_outbox");
        outbox.HasKey(value => value.EventId);
        outbox.Property(value => value.EventId).HasColumnName("event_id");
        outbox.Property(value => value.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        outbox.Property(value => value.AggregateId).HasColumnName("aggregate_id").HasMaxLength(100).IsRequired();
        outbox.Property(value => value.AggregateType).HasColumnName("aggregate_type").HasMaxLength(50).IsRequired();
        outbox.Property(value => value.Sequence).HasColumnName("sequence").IsRequired();
        outbox.Property(value => value.Version).HasColumnName("version").IsRequired();
        outbox.Property(value => value.PartitionKey).HasColumnName("partition_key").HasMaxLength(100).IsRequired();
        outbox.Property(value => value.Topic).HasColumnName("topic").HasMaxLength(100).IsRequired();
        outbox.Property(value => value.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        outbox.Property(value => value.OccurredAt).HasColumnName("occurred_at").IsRequired();
        outbox.Property(value => value.PublishedAt).HasColumnName("published_at");
        outbox.Property(value => value.Attempts).HasColumnName("attempts").IsRequired();
        outbox.Property(value => value.LastError).HasColumnName("last_error");
        outbox.HasIndex(value => new { value.PublishedAt, value.OccurredAt })
            .HasDatabaseName("ix_outbox_unpublished");
        outbox.HasIndex(value => new { value.Topic, value.PartitionKey, value.Sequence })
            .HasDatabaseName("ix_outbox_stream_sequence");

        var processed = modelBuilder.Entity<ProcessedIntegrationEvent>();
        processed.ToTable("processed_events");
        processed.HasKey(value => new { value.Consumer, value.EventId });
        processed.Property(value => value.Consumer).HasColumnName("consumer").HasMaxLength(100);
        processed.Property(value => value.EventId).HasColumnName("event_id");
        processed.Property(value => value.ProcessedAt).HasColumnName("processed_at").IsRequired();

        var position = modelBuilder.Entity<PortfolioPosition>();
        position.ToTable("portfolio_positions");
        position.HasKey(value => new { value.AccountId, value.Asset });
        position.Property(value => value.AccountId).HasColumnName("account_id");
        position.Property(value => value.Asset).HasColumnName("asset").HasMaxLength(20);
        position.Property(value => value.Quantity).HasColumnName("quantity").HasPrecision(30, 10).IsRequired();
        position.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();

        var market = modelBuilder.Entity<MarketProjection>();
        market.ToTable("market_projections");
        market.HasKey(value => value.Symbol);
        market.Property(value => value.Symbol).HasColumnName("symbol").HasMaxLength(30);
        market.Property(value => value.LastPrice).HasColumnName("last_price").HasPrecision(30, 10).IsRequired();
        market.Property(value => value.Volume).HasColumnName("volume").HasPrecision(30, 10).IsRequired();
        market.Property(value => value.TradeCount).HasColumnName("trade_count").IsRequired();
        market.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();

        var analytics = modelBuilder.Entity<AnalyticsProjection>();
        analytics.ToTable("analytics_projections");
        analytics.HasKey(value => value.Symbol);
        analytics.Property(value => value.Symbol).HasColumnName("symbol").HasMaxLength(30);
        analytics.Property(value => value.TradeCount).HasColumnName("trade_count").IsRequired();
        analytics.Property(value => value.TotalQuantity).HasColumnName("total_quantity").HasPrecision(30, 10).IsRequired();
        analytics.Property(value => value.TotalNotional).HasColumnName("total_notional").HasPrecision(30, 10).IsRequired();
        analytics.Property(value => value.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}