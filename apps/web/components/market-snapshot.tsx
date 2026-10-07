"use client";

import { useCallback, useEffect, useState } from "react";

type Health = {
  status: string;
  timestamp?: string;
};

type Market = {
  symbol: string;
  base_asset: string;
  quote_asset: string;
  last_price: string;
  change_percent_24h: string;
  volume_24h: string;
  status: string;
};

type MarketsResponse = {
  data: Market[];
};

export function MarketSnapshot() {
  const [health, setHealth] = useState<Health | null>(null);
  const [markets, setMarkets] = useState<Market[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const loadMarkets = useCallback(async (signal?: AbortSignal) => {
    try {
      const [healthResponse, marketsResponse] = await Promise.all([
        fetch("/api/health", { cache: "no-store", signal }),
        fetch("/api/markets", { cache: "no-store", signal }),
      ]);

      if (!healthResponse.ok || !marketsResponse.ok) {
        throw new Error("Market services are currently unavailable.");
      }

      const healthBody = (await healthResponse.json()) as Health;
      const marketsBody = (await marketsResponse.json()) as MarketsResponse;
      setHealth(healthBody);
      setMarkets(marketsBody.data);
      setError(null);
    } catch (reason) {
      if (signal?.aborted) return;
      setHealth(null);
      setMarkets([]);
      setError(reason instanceof Error ? reason.message : "The market snapshot could not be loaded.");
    } finally {
      if (!signal?.aborted) setLoading(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    const timeout = window.setTimeout(() => void loadMarkets(controller.signal), 0);
    return () => {
      window.clearTimeout(timeout);
      controller.abort();
    };
  }, [loadMarkets]);

  return (
    <section className="contentPanel">
      <div className="panelHeading">
        <div>
          <p className="eyebrow">SIMULATED MARKET DATA</p>
          <h1>Markets</h1>
          <p>Latest available prices and activity from the ABADAR backend.</p>
        </div>
        <div className="statusGroup">
          <span className={health?.status === "ok" ? "statusDot online" : "statusDot"} />
          <span>
            {error
              ? "Service unavailable"
              : health?.timestamp
                ? `Updated ${new Date(health.timestamp).toLocaleTimeString()}`
                : "Checking service"}
          </span>
          <button
            className="button buttonSecondary buttonSmall"
            onClick={() => {
              setLoading(true);
              void loadMarkets();
            }}
            type="button"
          >
            Refresh
          </button>
        </div>
      </div>

      {error && <p className="formAlert" role="alert">{error}</p>}

      <div className="dataTable" role="table" aria-label="Simulated markets">
        <div className="dataRow dataHeader" role="row">
          <span>Market</span>
          <span>Last price</span>
          <span>24h change</span>
          <span>24h volume</span>
          <span>Status</span>
        </div>
        {loading ? (
          <div className="emptyState">Loading markets…</div>
        ) : markets.length > 0 ? (
          markets.map((market) => {
            const negative = market.change_percent_24h.startsWith("-");
            return (
              <div className="dataRow" role="row" key={market.symbol}>
                <span className="marketName">
                  <b>{market.base_asset}</b><small> / {market.quote_asset}</small>
                </span>
                <span className="mono">${formatNumber(market.last_price)}</span>
                <span className={negative ? "marketChange negative" : "marketChange positive"}>
                  {market.change_percent_24h}%
                </span>
                <span className="mono">{formatNumber(market.volume_24h)}</span>
                <span className="marketStatus">{market.status}</span>
              </div>
            );
          })
        ) : (
          <div className="emptyState">No market data is available.</div>
        )}
      </div>
    </section>
  );
}

function formatNumber(value: string) {
  const parsed = Number(value);
  if (Number.isNaN(parsed)) return value;

  return new Intl.NumberFormat("en-US", {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(parsed);
}