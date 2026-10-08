using Abadar.Backend.Data;
using Abadar.Backend.Events;
using Abadar.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Abadar.Backend.Services;

// Applies trade events to one named projection inside an idempotent database boundary.
public sealed class TradeProjectionProcessor(AbadarDbContext dbContext)
{
    // Skips duplicate events and saves the projection together with its marker.
    public async Task<bool> ProcessAsync(
        string consumer,
        EventEnvelope<TradeExecutedEvent> envelope,
        CancellationToken cancellationToken)
    {
        var duplicate = await dbContext.ProcessedEvents.AnyAsync(
            value => value.Consumer == consumer && value.EventId == envelope.EventId,
            cancellationToken);
        if (duplicate)
        {
            return false;
        }

        if (consumer == "portfolio")
        {
            await ApplyPortfolioAsync(envelope, cancellationToken);
        }
        else if (consumer == "market")
        {
            await ApplyMarketAsync(envelope, cancellationToken);
        }
        else if (consumer == "analytics")
        {
            await ApplyAnalyticsAsync(envelope, cancellationToken);
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(consumer), consumer, "Unknown trade projection.");
        }

        dbContext.ProcessedEvents.Add(new ProcessedIntegrationEvent
        {
            Consumer = consumer,
            EventId = envelope.EventId,
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Applies signed base-asset quantity to buyer and seller positions.
    private async Task ApplyPortfolioAsync(
        EventEnvelope<TradeExecutedEvent> envelope,
        CancellationToken cancellationToken)
    {
        var baseAsset = envelope.Payload.Symbol.Split('/')[0];
        await UpdatePositionAsync(envelope.Payload.BuyerAccountId, baseAsset, envelope.Payload.Quantity, envelope.OccurredAt, cancellationToken);
        await UpdatePositionAsync(envelope.Payload.SellerAccountId, baseAsset, -envelope.Payload.Quantity, envelope.OccurredAt, cancellationToken);
    }

    // Creates or increments one account/asset position.
    private async Task UpdatePositionAsync(
        Guid accountId,
        string asset,
        decimal change,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var position = await dbContext.PortfolioPositions.SingleOrDefaultAsync(
            value => value.AccountId == accountId && value.Asset == asset,
            cancellationToken);
        if (position is null)
        {
            dbContext.PortfolioPositions.Add(new PortfolioPosition
            {
                AccountId = accountId,
                Asset = asset,
                Quantity = change,
                UpdatedAt = occurredAt
            });
            return;
        }

        position.Quantity += change;
        position.UpdatedAt = occurredAt;
    }

    // Applies one execution to the symbol ticker projection.
    private async Task ApplyMarketAsync(
        EventEnvelope<TradeExecutedEvent> envelope,
        CancellationToken cancellationToken)
    {
        var market = await dbContext.MarketProjections.SingleOrDefaultAsync(
            value => value.Symbol == envelope.Payload.Symbol,
            cancellationToken);
        if (market is null)
        {
            dbContext.MarketProjections.Add(new MarketProjection
            {
                Symbol = envelope.Payload.Symbol,
                LastPrice = envelope.Payload.Price,
                Volume = envelope.Payload.Quantity,
                TradeCount = 1,
                UpdatedAt = envelope.OccurredAt
            });
            return;
        }

        market.LastPrice = envelope.Payload.Price;
        market.Volume += envelope.Payload.Quantity;
        market.TradeCount++;
        market.UpdatedAt = envelope.OccurredAt;
    }

    // Applies one execution to the per-symbol analytics projection.
    private async Task ApplyAnalyticsAsync(
        EventEnvelope<TradeExecutedEvent> envelope,
        CancellationToken cancellationToken)
    {
        var analytics = await dbContext.AnalyticsProjections.SingleOrDefaultAsync(
            value => value.Symbol == envelope.Payload.Symbol,
            cancellationToken);
        if (analytics is null)
        {
            dbContext.AnalyticsProjections.Add(new AnalyticsProjection
            {
                Symbol = envelope.Payload.Symbol,
                TradeCount = 1,
                TotalQuantity = envelope.Payload.Quantity,
                TotalNotional = envelope.Payload.Price * envelope.Payload.Quantity,
                UpdatedAt = envelope.OccurredAt
            });
            return;
        }

        analytics.TradeCount++;
        analytics.TotalQuantity += envelope.Payload.Quantity;
        analytics.TotalNotional += envelope.Payload.Price * envelope.Payload.Quantity;
        analytics.UpdatedAt = envelope.OccurredAt;
    }
}