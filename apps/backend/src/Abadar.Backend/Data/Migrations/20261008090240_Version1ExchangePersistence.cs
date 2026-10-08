using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Abadar.Backend.Data.Migrations
{
    /// <inheritdoc />
    // Creates durable Version 1 order and trade history storage.
    public partial class Version1ExchangePersistence : Migration
    {
        /// <inheritdoc />
        // Adds order/trade tables and indexes needed for history and recovery.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    side = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    price = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    remaining_quantity = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trades",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    symbol = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    buy_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sell_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(30,10)", precision: 30, scale: 10, nullable: false),
                    sequence = table.Column<long>(type: "bigint", nullable: false),
                    executed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trades", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_orders_symbol_status",
                table: "orders",
                columns: new[] { "symbol", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_orders_symbol_sequence",
                table: "orders",
                columns: new[] { "symbol", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trades_executed_at",
                table: "trades",
                column: "executed_at");

            migrationBuilder.CreateIndex(
                name: "ux_trades_symbol_sequence",
                table: "trades",
                columns: new[] { "symbol", "sequence" },
                unique: true);
        }

        /// <inheritdoc />
        // Removes Version 1 exchange persistence objects during rollback.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "trades");
        }
    }
}
