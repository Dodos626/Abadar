using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abadar.Backend.Data.Migrations
{
    /// <inheritdoc />
    // Creates the outbox, idempotency, and consumer projection tables for Version 2.
    public partial class Version2EventDriven : Migration
    {
        /// <inheritdoc />
        // Adds the durable event and derived-state schema.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_projections",
                columns: table => new
                {
                    symbol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    trade_count = table.Column<long>(type: "bigint", nullable: false),
                    total_quantity = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    total_notional = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_projections", x => x.symbol);
                });

            migrationBuilder.CreateTable(
                name: "integration_event_outbox",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    partition_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    topic = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_event_outbox", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "market_projections",
                columns: table => new
                {
                    symbol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    last_price = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    volume = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    trade_count = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_market_projections", x => x.symbol);
                });

            migrationBuilder.CreateTable(
                name: "portfolio_positions",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    asset = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portfolio_positions", x => new { x.account_id, x.asset });
                });

            migrationBuilder.CreateTable(
                name: "processed_events",
                columns: table => new
                {
                    consumer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => new { x.consumer, x.event_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_stream_sequence",
                table: "integration_event_outbox",
                columns: new[] { "topic", "partition_key", "sequence" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_unpublished",
                table: "integration_event_outbox",
                columns: new[] { "published_at", "occurred_at" });
        }

        /// <inheritdoc />
        // Removes all Version 2 event-driven persistence objects.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_projections");

            migrationBuilder.DropTable(
                name: "integration_event_outbox");

            migrationBuilder.DropTable(
                name: "market_projections");

            migrationBuilder.DropTable(
                name: "portfolio_positions");

            migrationBuilder.DropTable(
                name: "processed_events");
        }
    }
}
