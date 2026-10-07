"use client";

import { Suspense, useEffect, useRef, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/components/auth-provider";

export default function LoginPage() {
  return (
    <Suspense fallback={<LoginLoading />}>
      <LoginForm />
    </Suspense>
  );
}

function LoginForm() {
  const { login, status } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const loginAttempt = useRef(false);

  useEffect(() => {
    if (status === "authenticated" && !loginAttempt.current) {
      router.replace("/dashboard");
    }
  }, [router, status]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    loginAttempt.current = true;
    setBusy(true);
    setMessage(null);

    try {
      await login(username, password);
      const requestedPath = searchParams.get("next");
      const destination = requestedPath?.startsWith("/") ? requestedPath : "/dashboard";
      router.replace(destination);
    } catch (error) {
      loginAttempt.current = false;
      setMessage(error instanceof Error ? error.message : "Login failed.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="authPage">
      <div className="authLayout pageContainer">
        <section className="authIntro">
          <p className="eyebrow">WELCOME BACK</p>
          <h1>Sign in to your ABADAR workspace.</h1>
          <p>Access your dashboard, market data, and account tools.</p>
          <div className="authFeatureList">
            <span>Role-aware workspace</span>
            <span>Protected account access</span>
            <span>Live platform information</span>
          </div>
        </section>

        <form className="loginCard" onSubmit={submit}>
          <div>
            <p className="eyebrow">ACCOUNT ACCESS</p>
            <h2>Login</h2>
          </div>

          <label>
            Username or email
            <input
              autoComplete="username"
              autoFocus
              required
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              placeholder="Enter your username"
            />
          </label>

          <label>
            Password
            <input
              autoComplete="current-password"
              required
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="Enter your password"
            />
          </label>

          {message && <p className="formAlert" role="alert">{message}</p>}

          <button className="button buttonPrimary buttonFull" disabled={busy} type="submit">
            {busy ? "Signing in…" : "Sign in"}
          </button>

          <div className="demoAccounts">
            <span>Development accounts</span>
            <code>admin / admin123</code>
            <code>user / user123</code>
          </div>

          <Link className="backLink" href="/">← Back to home</Link>
        </form>
      </div>
    </main>
  );
}

function LoginLoading() {
  return (
    <main className="centeredPage" aria-live="polite">
      <div className="loader" />
      <p>Loading login…</p>
    </main>
  );
}