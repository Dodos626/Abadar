"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { useAuth } from "@/components/auth-provider";
import {
  apiRequest,
  type OrderHistory,
  type ResetDatabaseResponse,
  type SimulationRequest,
  type SimulationResponse,
  type TradeHistory,
} from "@/lib/api";

// Lists the trading pairs currently accepted by the backend simulator.
const availableSymbols = ["BTC/USD", "ETH/USD", "SOL/USD", "ABR/USD"];

// Provides safe deterministic defaults for the administrator simulation form.
const initialForm: SimulationRequest = {
  symbols: ["BTC/USD"],
  min_trades: 25,
  max_trades: 50,
  min_price: 95,
  max_price: 105,
  min_quantity: 0.1,
  max_quantity: 2,
  sell_percentage: 50,
  market_order_percentage: 15,
  seed: 42,
};

// Renders simulation controls, durable history, and destructive reset operations.
export function MarketSimulator() {
  const { token } = useAuth();
  const [form, setForm] = useState<SimulationRequest>(initialForm);
  const [orders, setOrders] = useState<OrderHistory[]>([]);
  const [trades, setTrades] = useState<TradeHistory[]>([]);
  const [result, setResult] = useState<SimulationResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(true);
  const [message, setMessage] = useState<string | null>(null);

  // Filters history automatically when exactly one trading pair is selected.
  const selectedSymbol = useMemo(
    () => (form.symbols.length === 1 ? form.symbols[0] : undefined),
    [form.symbols],
  );

  // Loads the latest durable orders and trades for the current symbol selection.
  const loadHistory = useCallback(async () => {
    if (!token) return;
    setLoadingHistory(true);
    const query = selectedSymbol
      ? `?symbol=${encodeURIComponent(selectedSymbol)}&limit=50`
      : "?limit=50";
    try {
      const [nextOrders, nextTrades] = await Promise.all([
        apiRequest<OrderHistory[]>(`/orders${query}`, {}, token),
        apiRequest<TradeHistory[]>(`/trades${query}`, {}, token),
      ]);
      setOrders(nextOrders);
      setTrades(nextTrades);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "History could not be loaded.");
    } finally {
      setLoadingHistory(false);
    }
  }, [selectedSymbol, token]);

  // Refreshes history when authentication or the single-symbol filter changes.
  useEffect(() => {
    const timeout = window.setTimeout(() => void loadHistory(), 0);
    return () => window.clearTimeout(timeout);
  }, [loadHistory]);

  // Submits the configured simulation and refreshes its resulting history.
  async function simulate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) return;
    setBusy(true);
    setMessage(null);
    try {
      const response = await apiRequest<SimulationResponse>(
        "/admin/simulations",
        { method: "POST", body: JSON.stringify(form) },
        token,
      );
      setResult(response);
      setMessage(`Submitted ${response.orders_submitted} orders and executed ${response.trades_executed} trades.`);
      await loadHistory();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "The simulation could not be started.");
    } finally {
      setBusy(false);
    }
  }

  // Requires explicit confirmation before deleting all non-calling test data.
  async function resetDatabase() {
    if (!token) return;
    const confirmation = window.prompt(
      "This deletes every order, trade, and user except your administrator account. Type RESET to continue.",
    );
    if (confirmation !== "RESET") return;

    setBusy(true);
    setMessage(null);
    try {
      const response = await apiRequest<ResetDatabaseResponse>(
        "/admin/database/reset",
        { method: "POST" },
        token,
      );
      setResult(null);
      setOrders([]);
      setTrades([]);
      setMessage(
        `Database reset: ${response.users_deleted} users, ${response.orders_deleted} orders, and ${response.trades_deleted} trades removed.`,
      );
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "The database could not be reset.");
    } finally {
      setBusy(false);
    }
  }

  // Adds or removes one trading pair from the simulation request.
  function toggleSymbol(symbol: string) {
    setForm((current) => ({
      ...current,
      symbols: current.symbols.includes(symbol)
        ? current.symbols.filter((value) => value !== symbol)
        : [...current.symbols, symbol],
    }));
  }

  return (
    <div className="adminStack">
      <section className="contentPanel">
        <div className="panelHeading">
          <div>
            <p className="eyebrow">VERSION 1 OPERATIONS</p>
            <h1>Market simulator</h1>
            <p>Generate durable buy and sell activity through the production matching engine.</p>
          </div>
          <button className="button buttonSecondary buttonSmall" onClick={() => void loadHistory()} type="button">
            Refresh history
          </button>
        </div>

        <form className="formLayout" onSubmit={simulate}>
          <fieldset className="symbolPicker">
            <legend>Trading pairs</legend>
            <div>
              {availableSymbols.map((symbol) => (
                <label className="checkPill" key={symbol}>
                  <input
                    checked={form.symbols.includes(symbol)}
                    onChange={() => toggleSymbol(symbol)}
                    type="checkbox"
                  />
                  {symbol}
                </label>
              ))}
            </div>
          </fieldset>

          <div className="formGrid adminFormGrid">
            <NumberField label="Minimum orders" min={1} value={form.min_trades} onChange={(value) => setForm({ ...form, min_trades: value })} />
            <NumberField label="Maximum orders" min={1} value={form.max_trades} onChange={(value) => setForm({ ...form, max_trades: value })} />
            <NumberField label="Sell percentage" min={0} max={100} value={form.sell_percentage} onChange={(value) => setForm({ ...form, sell_percentage: value })} />
            <NumberField label="Minimum price" min={0.00000001} step="any" value={form.min_price} onChange={(value) => setForm({ ...form, min_price: value })} />
            <NumberField label="Maximum price" min={0.00000001} step="any" value={form.max_price} onChange={(value) => setForm({ ...form, max_price: value })} />
            <NumberField label="Market order percentage" min={0} max={100} value={form.market_order_percentage} onChange={(value) => setForm({ ...form, market_order_percentage: value })} />
            <NumberField label="Minimum quantity" min={0.00000001} step="any" value={form.min_quantity} onChange={(value) => setForm({ ...form, min_quantity: value })} />
            <NumberField label="Maximum quantity" min={0.00000001} step="any" value={form.max_quantity} onChange={(value) => setForm({ ...form, max_quantity: value })} />
            <NumberField label="Random seed" value={form.seed ?? 0} onChange={(value) => setForm({ ...form, seed: value })} />
          </div>

          <div className="formActions">
            <button className="button buttonPrimary" disabled={busy || form.symbols.length === 0} type="submit">
              {busy ? "Running…" : "Run simulation"}
            </button>
            <button className="button buttonDanger" disabled={busy} onClick={() => void resetDatabase()} type="button">
              Reset database
            </button>
          </div>
        </form>

        {message && <p className="formNotice" role="status">{message}</p>}
        {result && (
          <div className="simulationSummary">
            {result.symbols.map((symbol) => (
              <article key={symbol.symbol}>
                <b>{symbol.symbol}</b>
                <span>{symbol.orders_submitted} orders</span>
                <span>{symbol.trades_executed} trades</span>
                <span>{formatNumber(symbol.executed_quantity)} volume</span>
              </article>
            ))}
          </div>
        )}
      </section>

      <HistoryTable title="Order history" loading={loadingHistory} empty="No durable orders yet." hasRows={orders.length > 0}>
        <div className="historyRow historyHeader orderHistoryRow">
          <span>Symbol</span><span>Side</span><span>Type</span><span>Price</span><span>Quantity / remaining</span><span>Status</span>
        </div>
        {orders.map((order) => (
          <div className="historyRow orderHistoryRow" key={order.id}>
            <b>{order.symbol}</b>
            <span className={order.side === "buy" ? "marketChange positive" : "marketChange negative"}>{order.side}</span>
            <span>{order.type}</span>
            <span className="mono">{order.price === null ? "Market" : formatNumber(order.price)}</span>
            <span className="mono">{formatNumber(order.quantity)} / {formatNumber(order.remaining_quantity)}</span>
            <span>{order.status.replaceAll("_", " ")}</span>
          </div>
        ))}
      </HistoryTable>

      <HistoryTable title="Trade history" loading={loadingHistory} empty="No durable trades yet." hasRows={trades.length > 0}>
        <div className="historyRow historyHeader tradeHistoryRow">
          <span>Symbol</span><span>Price</span><span>Quantity</span><span>Sequence</span><span>Executed</span>
        </div>
        {trades.map((trade) => (
          <div className="historyRow tradeHistoryRow" key={trade.id}>
            <b>{trade.symbol}</b>
            <span className="mono">{formatNumber(trade.price)}</span>
            <span className="mono">{formatNumber(trade.quantity)}</span>
            <span className="mono">#{trade.sequence}</span>
            <span>{new Date(trade.executed_at).toLocaleString()}</span>
          </div>
        ))}
      </HistoryTable>
    </div>
  );
}

// Renders a reusable numeric simulation input with browser constraints.
function NumberField({ label, value, onChange, min, max, step = "1" }: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  min?: number;
  max?: number;
  step?: string;
}) {
  return (
    <label>
      {label}
      <input min={min} max={max} required step={step} type="number" value={value} onChange={(event) => onChange(Number(event.target.value))} />
    </label>
  );
}

// Renders a common loading, empty, or populated durable-history panel.
function HistoryTable({ title, loading, empty, hasRows, children }: {
  title: string;
  loading: boolean;
  empty: string;
  hasRows: boolean;
  children: ReactNode;
}) {
  return (
    <section className="contentPanel">
      <div className="tableHeading"><h2>{title}</h2></div>
      <div className="historyTable">
        {loading ? <div className="emptyState">Loading history…</div> : hasRows ? children : <div className="emptyState">{empty}</div>}
      </div>
    </section>
  );
}

// Formats financial values without introducing extra decimal rounding.
function formatNumber(value: number) {
  return new Intl.NumberFormat("en-US", { maximumFractionDigits: 8 }).format(value);
}