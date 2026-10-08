"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent, type ReactNode } from "react";
import { HubConnectionBuilder, HttpTransportType, LogLevel, type HubConnection } from "@microsoft/signalr";
import { useAuth } from "@/components/auth-provider";
import {
  apiRequest,
  type EventReplayResponse,
  type EventSystemStatus,
  type OrderHistory,
  type ResetDatabaseResponse,
  type SimulationRequest,
  type SimulationProgress,
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

const simulationSteps = [
  {
    id: "browser",
    title: "1. Admin request",
    invokes: "MarketSimulator.simulate",
    detail: "The form creates a run ID, opens SignalR, and POSTs the selected symbols and generation ranges.",
  },
  {
    id: "endpoint",
    title: "2. Authorized API",
    invokes: "POST /api/v1/admin/simulations",
    detail: "JWT role checks allow only administrators before ExchangeService receives the request.",
  },
  {
    id: "generate",
    title: "3. Order generation",
    invokes: "ExchangeService.SimulateAsync",
    detail: "A deterministic random generator chooses count, side, type, price, and quantity for each symbol.",
  },
  {
    id: "match",
    title: "4. Matching engine",
    invokes: "MatchingEngine.SubmitAsync",
    detail: "The symbol worker serializes mutations and OrderBook applies price-time priority and creates executions.",
  },
  {
    id: "persist",
    title: "5. Durable batch",
    invokes: "FlushPersistenceBatchAsync",
    detail: "Orders, trades, resting-order changes, and event-outbox rows commit to PostgreSQL every 250 orders.",
  },
  {
    id: "events",
    title: "6. Kafka publication",
    invokes: "OutboxPublisher",
    detail: "The background publisher sends committed outbox rows to symbol-keyed Kafka topics and marks acknowledgements.",
  },
  {
    id: "consumers",
    title: "7. Derived projections",
    invokes: "Portfolio / Market / Analytics consumers",
    detail: "Independent consumer groups apply trade events once using processed_events idempotency markers.",
  },
];

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
  const [eventStatus, setEventStatus] = useState<EventSystemStatus | null>(null);
  const [progress, setProgress] = useState<SimulationProgress | null>(null);
  const [progressLog, setProgressLog] = useState<SimulationProgress[]>([]);

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

  // Loads Kafka, outbox, and consumer projection progress for administrators.
  const loadEventStatus = useCallback(async () => {
    if (!token) return;
    try {
      setEventStatus(await apiRequest<EventSystemStatus>("/admin/events/status", {}, token));
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Event status could not be loaded.");
    }
  }, [token]);

  // Refreshes history when authentication or the single-symbol filter changes.
  useEffect(() => {
    const timeout = window.setTimeout(() => {
      void loadHistory();
      void loadEventStatus();
    }, 0);
    return () => window.clearTimeout(timeout);
  }, [loadEventStatus, loadHistory]);

  // Submits the configured simulation and refreshes its resulting history.
  async function simulate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) return;
    const simulationId = crypto.randomUUID();
    let connection: HubConnection | null = null;
    setBusy(true);
    setMessage(null);
    setResult(null);
    setProgressLog([]);
    setProgress({
      simulation_id: simulationId,
      phase: "connecting",
      message: "Opening the administrator progress stream before invoking the simulation API.",
      symbol: null,
      current_symbol: 0,
      total_symbols: form.symbols.length,
      orders_generated: 0,
      orders_processed: 0,
      total_orders: 0,
      trades_executed: 0,
      executed_quantity: 0,
      percent: 1,
      occurred_at: new Date().toISOString(),
    });
    try {
      connection = new HubConnectionBuilder()
        .withUrl("http://localhost:8080/hubs/simulations", {
          accessTokenFactory: () => token,
          transport: HttpTransportType.WebSockets,
        })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();
      connection.on("simulation_progress", (update: SimulationProgress) => {
        if (update.simulation_id !== simulationId) return;
        setProgress(update);
        setProgressLog((current) => [...current.slice(-7), update]);
        return undefined;
      });
      await connection.start();
      await connection.invoke("WatchSimulation", simulationId);

      const response = await apiRequest<SimulationResponse>(
        "/admin/simulations",
        { method: "POST", body: JSON.stringify({ ...form, simulation_id: simulationId }) },
        token,
      );
      setResult(response);
      setMessage(`Submitted ${response.orders_submitted} orders and executed ${response.trades_executed} trades.`);
      await loadHistory();
      await loadEventStatus();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "The simulation could not be started.");
    } finally {
      if (connection) {
        try {
          await connection.invoke("StopWatchingSimulation", simulationId);
        } catch {
          // The connection may already be closed after a backend or network failure.
        }
        await connection.stop();
      }
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

  // Clears one projection and asks its Kafka consumer to replay the trade stream.
  async function replay(consumer: string) {
    if (!token || !window.confirm(`Rebuild the ${consumer} projection from Kafka?`)) return;
    setBusy(true);
    try {
      const response = await apiRequest<EventReplayResponse>(
        `/admin/events/replay/${consumer}`,
        { method: "POST" },
        token,
      );
      setMessage(`Rebuilt ${response.consumer} from ${response.events_replayed} Kafka events after clearing ${response.processed_events_cleared} markers.`);
      await loadEventStatus();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Replay could not be requested.");
    } finally {
      setBusy(false);
    }
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

        <div className="simulationExplainer">
          <div className="explainerHeading">
            <div>
              <p className="eyebrow">HOW A RUN FLOWS</p>
              <h2>From the button to Kafka projections</h2>
            </div>
            <span>Each card names the code or endpoint invoked next.</span>
          </div>
          <div className="simulationFlowGrid">
            {simulationSteps.map((step) => (
              <article className={stepClassName(step.id, progress?.phase)} key={step.id}>
                <span className="flowStatus" />
                <h3>{step.title}</h3>
                <code>{step.invokes}</code>
                <p>{step.detail}</p>
              </article>
            ))}
          </div>
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

        {(busy || progress) && (
          <section className="simulationProgress" aria-live="polite">
            <div className="progressHeading">
              <div>
                <p className="eyebrow">LIVE SIMULATION</p>
                <h2>{progress?.message ?? "Preparing simulation…"}</h2>
              </div>
              <strong>{Math.round(progress?.percent ?? 0)}%</strong>
            </div>
            <div
              className="progressTrack"
              role="progressbar"
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={Math.round(progress?.percent ?? 0)}
            >
              <span style={{ width: `${progress?.percent ?? 0}%` }} />
            </div>
            <div className="progressMetrics">
              <article><span>Symbol</span><b>{progress?.symbol ?? "All"}</b></article>
              <article><span>Generated</span><b>{formatInteger(progress?.orders_generated ?? 0)}</b></article>
              <article><span>Processed</span><b>{formatInteger(progress?.orders_processed ?? 0)} / {formatInteger(progress?.total_orders ?? 0)}</b></article>
              <article><span>Trades</span><b>{formatInteger(progress?.trades_executed ?? 0)}</b></article>
              <article><span>Volume</span><b>{formatNumber(progress?.executed_quantity ?? 0)}</b></article>
            </div>
            {progressLog.length > 0 && (
              <div className="progressLog">
                {progressLog.map((entry, index) => (
                  <div key={`${entry.occurred_at}-${index}`}>
                    <span>{new Date(entry.occurred_at).toLocaleTimeString()}</span>
                    <b>{entry.phase.replaceAll("_", " ")}</b>
                    <p>{entry.message}</p>
                  </div>
                ))}
              </div>
            )}
          </section>
        )}

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

      <section className="contentPanel">
        <div className="panelHeading">
          <div>
            <p className="eyebrow">VERSION 2 EVENT STREAM</p>
            <h2>Kafka and consumers</h2>
            <p>Inspect durable publication and rebuild eventually consistent projections.</p>
          </div>
          <button className="button buttonSecondary buttonSmall" onClick={() => void loadEventStatus()} type="button">
            Refresh events
          </button>
        </div>
        {eventStatus ? (
          <div className="eventStatusGrid">
            <article><b>Kafka</b><span>{eventStatus.topics_ready ? "Topics ready" : "Starting"}</span></article>
            <article><b>Outbox pending</b><span>{eventStatus.outbox_pending}</span></article>
            <article><b>Published</b><span>{eventStatus.outbox_published}</span></article>
            {eventStatus.consumers.map((consumer) => (
              <article key={consumer.consumer}>
                <b>{consumer.consumer}</b>
                <span>{consumer.processed_events} processed events</span>
                <button className="textButton" disabled={busy} onClick={() => void replay(consumer.consumer)} type="button">
                  Replay
                </button>
              </article>
            ))}
          </div>
        ) : (
          <div className="emptyState">Loading event status…</div>
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

// Formats order and trade counters without financial decimal places.
function formatInteger(value: number) {
  return new Intl.NumberFormat("en-US", { maximumFractionDigits: 0 }).format(value);
}

// Maps backend progress phases onto the explanatory architecture cards.
function stepClassName(step: string, phase?: string) {
  const phaseOrder: Record<string, number> = {
    connecting: 0,
    validating: 1,
    generated: 2,
    persisting: 4,
    symbol_complete: 4,
    completed: 6,
    failed: 4,
  };
  const stepOrder: Record<string, number> = {
    browser: 0,
    endpoint: 1,
    generate: 2,
    match: 3,
    persist: 4,
    events: 5,
    consumers: 6,
  };
  if (!phase) return "simulationFlowCard";
  const current = phaseOrder[phase] ?? 0;
  const order = stepOrder[step];
  return `simulationFlowCard ${order < current ? "complete" : order === current ? "active" : "pending"}`;
}