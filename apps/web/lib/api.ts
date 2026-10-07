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