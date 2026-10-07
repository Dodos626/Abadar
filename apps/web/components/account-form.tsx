"use client";

import { useState, type FormEvent } from "react";
import { useAuth } from "@/components/auth-provider";
import { apiRequest, type User } from "@/lib/api";

export function AccountForm() {
  const { token, user, updateUser } = useAuth();
  const activeToken = token;
  const activeUser = user;
  const [firstName, setFirstName] = useState(user?.first_name ?? "");
  const [lastName, setLastName] = useState(user?.last_name ?? "");
  const [email, setEmail] = useState(user?.email ?? "");
  const [username, setUsername] = useState(user?.username ?? "");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (!activeUser || !activeToken) return null;

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setMessage(null);

    try {
      const saved = await apiRequest<User>(
        "/users/me",
        {
          method: "PUT",
          body: JSON.stringify({
            first_name: firstName,
            last_name: lastName,
            email,
            username,
            password: password || null,
          }),
        },
        activeToken!,
      );
      updateUser(saved);
      setPassword("");
      setMessage("Your account has been updated.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Your account could not be updated.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="contentPanel narrowPanel">
      <div className="panelHeading">
        <div>
          <p className="eyebrow">PERSONAL SETTINGS</p>
          <h1>Account</h1>
          <p>Keep your profile and sign-in details up to date.</p>
        </div>
        <span className={`rolePill ${activeUser.role}`}>{activeUser.role}</span>
      </div>

      <form className="formLayout" onSubmit={submit}>
        <div className="formGrid">
          <FormField label="First name" value={firstName} onChange={setFirstName} />
          <FormField label="Last name" value={lastName} onChange={setLastName} />
          <FormField label="Email" type="email" value={email} onChange={setEmail} />
          <FormField label="Username" value={username} onChange={setUsername} />
          <FormField
            label="New password (optional)"
            type="password"
            value={password}
            onChange={setPassword}
          />
        </div>

        {message && <p className={message.includes("updated") ? "formSuccess" : "formAlert"}>{message}</p>}

        <button className="button buttonPrimary" disabled={busy} type="submit">
          {busy ? "Saving…" : "Save changes"}
        </button>
      </form>
    </section>
  );
}

function FormField({
  label,
  value,
  onChange,
  type = "text",
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
}) {
  return (
    <label>
      {label}
      <input required={type !== "password"} type={type} value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}