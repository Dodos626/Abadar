export type UserRole = "admin" | "user";

export type User = {
  id: string;
  first_name: string;
  last_name: string;
  last_online: string | null;
  email: string;
  username: string;
  role: UserRole;
  created_at: string;
  updated_at: string;
};

export type LoginResponse = {
  access_token: string;
  expires_at: string;
  user: User;
};

export type UserInput = {
  first_name: string;
  last_name: string;
  email: string;
  username: string;
  password?: string;
  role: UserRole;
};

export type ApiError = {
  error?: {
    code?: string;
    message?: string;
    details?: Record<string, string[]>;
  };
};

// Defines the administrator controls sent to the market simulation endpoint.
export type SimulationRequest = {
  symbols: string[];
  min_trades: number;
  max_trades: number;
  min_price: number;
  max_price: number;
  min_quantity: number;
  max_quantity: number;
  sell_percentage: number;
  market_order_percentage: number;
  seed?: number;
};

// Describes the activity generated for one requested trading pair.
export type SimulationSymbolResult = {
  symbol: string;
  orders_submitted: number;
  trades_executed: number;
  executed_quantity: number;
  last_price: number | null;
};

// Returns aggregate and per-symbol market simulation totals.
export type SimulationResponse = {
  orders_submitted: number;
  trades_executed: number;
  symbols: SimulationSymbolResult[];
};

// Models one durable order row returned by the Version 1 API.
export type OrderHistory = {
  id: string;
  symbol: string;
  side: string;
  type: string;
  price: number | null;
  quantity: number;
  remaining_quantity: number;
  status: string;
  sequence: number;
  created_at: string;
};

// Models one immutable trade row returned by the Version 1 API.
export type TradeHistory = {
  id: string;
  symbol: string;
  price: number;
  quantity: number;
  sequence: number;
  executed_at: string;
};

// Reports the records removed by the administrator reset action.
export type ResetDatabaseResponse = {
  preserved_admin_id: string;
  users_deleted: number;
  orders_deleted: number;
  trades_deleted: number;
};

// Reports one consumer projection's processed-event count.
export type ConsumerProgress = {
  consumer: string;
  processed_events: number;
};

// Reports Kafka, outbox, and consumer progress for Version 2 operations.
export type EventSystemStatus = {
  kafka_enabled: boolean;
  topics_ready: boolean;
  publisher_connected: boolean;
  last_error: string | null;
  outbox_pending: number;
  outbox_published: number;
  consumers: ConsumerProgress[];
};

// Reports a requested projection replay reset.
export type EventReplayResponse = {
  consumer: string;
  processed_events_cleared: number;
  events_replayed: number;
  requested_at: string;
};

export class ApiRequestError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = "ApiRequestError";
  }
}

export async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
  token?: string,
): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("content-type", "application/json");

  if (token) {
    headers.set("authorization", `Bearer ${token}`);
  }

  const response = await fetch(`/api/backend${path}`, {
    ...init,
    headers,
    cache: "no-store",
  });

  if (!response.ok) {
    const body = (await response.json().catch(() => ({}))) as ApiError;
    throw new ApiRequestError(
      body.error?.message ?? `Request failed (${response.status}).`,
      response.status,
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}