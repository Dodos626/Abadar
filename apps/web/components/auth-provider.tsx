"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import {
  ApiRequestError,
  apiRequest,
  type LoginResponse,
  type User,
} from "@/lib/api";

const sessionKey = "abadar.session";

type StoredSession = {
  accessToken: string;
  expiresAt: string;
  user: User;
};

type AuthContextValue = {
  status: "loading" | "authenticated" | "anonymous";
  token: string | null;
  user: User | null;
  login: (username: string, password: string) => Promise<User>;
  logout: () => void;
  updateUser: (user: User) => void;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(null);
  const [status, setStatus] = useState<AuthContextValue["status"]>("loading");

  const clearSession = useCallback(() => {
    window.localStorage.removeItem(sessionKey);
    setSession(null);
    setStatus("anonymous");
  }, []);

  const storeSession = useCallback((nextSession: StoredSession) => {
    window.localStorage.setItem(sessionKey, JSON.stringify(nextSession));
    setSession(nextSession);
    setStatus("authenticated");
  }, []);

  useEffect(() => {
    let active = true;

    async function restoreSession() {
      const stored = window.localStorage.getItem(sessionKey);

      if (!stored) {
        if (active) setStatus("anonymous");
        return;
      }

      try {
        const parsed = JSON.parse(stored) as StoredSession;
        const expiresAt = Date.parse(parsed.expiresAt);

        if (
          !parsed.accessToken ||
          !parsed.user ||
          Number.isNaN(expiresAt) ||
          expiresAt <= Date.now()
        ) {
          throw new Error("The stored session is invalid or expired.");
        }

        const user = await apiRequest<User>("/users/me", {}, parsed.accessToken);
        if (active) {
          storeSession({ ...parsed, user });
        }
      } catch {
        if (active) clearSession();
      }
    }

    void restoreSession();

    return () => {
      active = false;
    };
  }, [clearSession, storeSession]);

  useEffect(() => {
    function syncSession(event: StorageEvent) {
      if (event.key !== sessionKey) return;

      if (!event.newValue) {
        setSession(null);
        setStatus("anonymous");
        return;
      }

      try {
        const nextSession = JSON.parse(event.newValue) as StoredSession;
        setSession(nextSession);
        setStatus("authenticated");
      } catch {
        clearSession();
      }
    }

    window.addEventListener("storage", syncSession);
    return () => window.removeEventListener("storage", syncSession);
  }, [clearSession]);

  const login = useCallback(
    async (username: string, password: string) => {
      try {
        const response = await apiRequest<LoginResponse>("/auth/login", {
          method: "POST",
          body: JSON.stringify({ username, password }),
        });

        storeSession({
          accessToken: response.access_token,
          expiresAt: response.expires_at,
          user: response.user,
        });

        return response.user;
      } catch (error) {
        if (error instanceof ApiRequestError) throw error;
        throw new Error("Unable to reach the authentication service.");
      }
    },
    [storeSession],
  );

  const updateUser = useCallback(
    (user: User) => {
      if (!session) return;
      storeSession({ ...session, user });
    },
    [session, storeSession],
  );

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      token: session?.accessToken ?? null,
      user: session?.user ?? null,
      login,
      logout: clearSession,
      updateUser,
    }),
    [clearSession, login, session, status, updateUser],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }

  return context;
}