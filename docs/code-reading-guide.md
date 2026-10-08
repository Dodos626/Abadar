# ABADAR Code Reading Guide

This guide gives you an order for reading ABADAR so that each layer explains the next one instead of exposing every framework at once.

## 1. Start with the pure matching rules

Read these files first:

1. `apps/matching-engine/src/Abadar.MatchingEngine/Domain.cs`
2. `apps/matching-engine/src/Abadar.MatchingEngine/OrderBook.cs`
3. `apps/matching-engine/tests/Abadar.MatchingEngine.Tests/OrderBookTests.cs`

Questions to answer while reading:

* What information enters the engine in `OrderRequest`?
* Why does an execution use the resting order's price?
* How do the sorted dictionaries implement best-price priority?
* How does the linked list implement FIFO within one price?
* Why is `_seenOrderIds` different from `_orders`?

Run only these tests while experimenting:

```bash
dotnet test apps/matching-engine/tests/Abadar.MatchingEngine.Tests/Abadar.MatchingEngine.Tests.csproj
```

## 2. Learn symbol ownership and concurrency

Read:

1. `apps/matching-engine/src/Abadar.MatchingEngine/MatchingEngine.cs`
2. `apps/matching-engine/tests/Abadar.MatchingEngine.Tests/MatchingEngineConcurrencyTests.cs`

Follow one `SubmitAsync` call from the public engine into `SymbolWorker`, through the channel, and finally into `OrderBook.Submit`.

Important idea:

> One channel reader owns one symbol's mutable order book, so the order book does not need locks.

## 3. Follow one Version 1 simulation order

Read in this order:

1. `apps/web/components/market-simulator.tsx`
2. `apps/web/lib/api.ts`
3. `apps/web/app/api/backend/[...path]/route.ts`
4. `apps/backend/src/Abadar.Backend/Endpoints/ExchangeEndpoints.cs`
5. `apps/backend/src/Abadar.Backend/Services/ExchangeService.cs`

Trace:

```text
Run simulation button
  → Next.js API proxy
  → POST /api/v1/admin/simulations
  → ExchangeService.SimulateAsync
  → MatchingEngine.SubmitAsync
  → ExchangeService.StageDurableOrderAsync
  → PostgreSQL batch commit
```

Pay special attention to the mutation gate, batching, EF Core tracking, and engine recovery after a failed persistence operation.

## 4. Understand Version 1 persistence and recovery

Read:

1. `apps/backend/src/Abadar.Backend/Models/ExchangeOrder.cs`
2. `apps/backend/src/Abadar.Backend/Models/ExchangeTrade.cs`
3. `apps/backend/src/Abadar.Backend/Data/AbadarDbContext.cs`
4. `apps/backend/src/Abadar.Backend/Data/Migrations/20261008090240_Version1ExchangePersistence.cs`
5. `ExchangeService.RecoverAsync`

Then inspect the integration test `DurableOpenOrders_RecoverAfterBackendRestart` in `BackendApiTests.cs`.

## 5. Learn the Version 2 event contracts

Read:

1. `apps/backend/src/Abadar.Backend/Events/IntegrationEvents.cs`
2. `apps/backend/src/Abadar.Backend/Models/IntegrationEventOutbox.cs`
3. `apps/backend/src/Abadar.Backend/Models/ProcessedIntegrationEvent.cs`

Understand the distinction:

* `event_id` gives global event identity.
* `event_type` includes the schema version.
* `partition_key` is the trading symbol.
* `sequence` preserves domain ordering within a symbol.
* the outbox row is written in the same database commit as orders and trades.

## 6. Follow an event from PostgreSQL to Kafka

Read:

1. `ExchangeService.StageDurableOrderAsync`
2. `apps/backend/src/Abadar.Backend/Services/OutboxPublisher.cs`
3. `apps/backend/src/Abadar.Backend/Services/KafkaTopicInitializer.cs`
4. `docker-compose.yml`

Trace:

```text
matching result
  → order/trade rows + outbox rows
  → PostgreSQL commit
  → OutboxPublisher polling
  → Kafka produce with symbol key
  → published_at recorded
```

This is the transactional-outbox pattern. PostgreSQL makes the business state and intent-to-publish atomic; Kafka publication is retried asynchronously.

## 7. Follow a trade through all consumers

Read:

1. `apps/backend/src/Abadar.Backend/Services/TradeEventConsumers.cs`
2. `apps/backend/src/Abadar.Backend/Services/TradeProjectionProcessor.cs`
3. `PortfolioPosition.cs`
4. `MarketProjection.cs`
5. `AnalyticsProjection.cs`

All three consumers read `abadar.trades`, but separate consumer groups let each projection independently receive every event.

The critical order is:

```text
consume
  → check processed_events
  → update projection
  → insert processed_events marker
  → save PostgreSQL transaction
  → commit Kafka offset
```

If Kafka redelivers an event after a crash, the marker prevents a second business update.

## 8. Understand replay

Read:

1. `apps/backend/src/Abadar.Backend/Services/EventReplayService.cs`
2. `apps/backend/src/Abadar.Backend/Services/EventReplayCoordinator.cs`
3. the replay controls in `market-simulator.tsx`

Replay deletes one derived projection and its processed-event markers, then creates a dedicated replay consumer that reads every retained trade partition from its beginning and rebuilds the selected projection.

The authoritative order/trade tables are not deleted during replay because the projections are derived state.

## 9. Read composition last

Finally read:

1. `apps/backend/src/Abadar.Backend/Program.cs`
2. `docker-compose.yml`
3. `apps/backend/src/Abadar.Backend/Endpoints/SystemEndpoints.cs`

These files show how PostgreSQL, Kafka, hosted publishers, consumers, authentication, and HTTP endpoints are assembled.

## 10. Suggested experiments

1. Add a log line containing `event_id` in `OutboxPublisher` and follow it into a consumer.
2. Send the same event twice to `TradeProjectionProcessor` and observe that the second call returns `false`.
3. Stop Kafka, run a simulation, and inspect pending outbox rows.
4. Restart Kafka and verify the outbox drains without losing the simulation.
5. Replay the analytics projection and compare the rebuilt totals with trade history.
6. Change a topic key from symbol to order ID in a branch and explain which ordering guarantee is lost.

## Useful commands

```bash
docker compose up --build
make kafka-topics
make test
make build
docker compose logs -f backend kafka
```