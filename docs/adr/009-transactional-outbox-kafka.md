# ADR-009 — Publish Kafka Events Through a PostgreSQL Outbox

## Status

Accepted for Version 2.

## Context

The matching engine updates PostgreSQL and must also publish integration events. Writing PostgreSQL and Kafka independently creates a dual-write failure window.

## Decision

The order/trade changes and serialized integration events are committed in one PostgreSQL transaction. A background publisher sends unpublished outbox rows to Kafka and marks them published only after broker acknowledgement.

Kafka delivery remains at least once. Consumers use `processed_events` with a `(consumer, event_id)` primary key so a duplicate event cannot update one projection twice.

## Consequences

* A successful database commit never loses the intent to publish.
* Kafka outages accumulate pending outbox rows instead of losing events.
* Duplicate Kafka records are expected and safe.
* Event publication is eventually consistent with matching persistence.
* The outbox requires retention and operational monitoring later.

## Partitioning

The Kafka message key is the trading symbol. This preserves order within one symbol while allowing different symbols to occupy different partitions.