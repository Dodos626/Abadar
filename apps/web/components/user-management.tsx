"use client";

import { useCallback, useEffect, useMemo, useState, type FormEvent } from "react";
import { useAuth } from "@/components/auth-provider";
import { apiRequest, type User, type UserInput, type UserRole } from "@/lib/api";

const emptyForm: UserInput = {
  first_name: "",
  last_name: "",
  email: "",
  username: "",
  password: "",
  role: "user",
};

export function UserManagement() {
  const { token, user: currentUser } = useAuth();
  const [users, setUsers] = useState<User[]>([]);
  const [form, setForm] = useState<UserInput>(emptyForm);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const sortedUsers = useMemo(
    () => [...users].sort((a, b) => a.username.localeCompare(b.username)),
    [users],
  );

  const loadUsers = useCallback(async () => {
    if (!token) return;
    try {
      setUsers(await apiRequest<User[]>("/users", {}, token));
      setMessage(null);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Users could not be loaded.");
    } finally {
      setLoading(false);
    }
  }, [token]);

  useEffect(() => {
    const timeout = window.setTimeout(() => void loadUsers(), 0);
    return () => window.clearTimeout(timeout);
  }, [loadUsers]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!token) return;
    setBusy(true);
    setMessage(null);

    try {
      const saved = await apiRequest<User>(
        editingId ? `/users/${editingId}` : "/users",
        {
          method: editingId ? "PUT" : "POST",
          body: JSON.stringify({
            ...form,
            password: editingId && !form.password ? null : form.password,
          }),
        },
        token,
      );
      setMessage(`${saved.username} has been ${editingId ? "updated" : "created"}.`);
      resetForm();
      await loadUsers();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "The user could not be saved.");
    } finally {
      setBusy(false);
    }
  }

  function editUser(user: User) {
    setEditingId(user.id);
    setForm({
      first_name: user.first_name,
      last_name: user.last_name,
      email: user.email,
      username: user.username,
      password: "",
      role: user.role,
    });
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  function resetForm() {
    setEditingId(null);
    setForm(emptyForm);
  }

  async function deleteUser(user: User) {
    if (!token || !window.confirm(`Delete ${user.first_name} ${user.last_name}?`)) return;

    try {
      await apiRequest<void>(`/users/${user.id}`, { method: "DELETE" }, token);
      setUsers((current) => current.filter((item) => item.id !== user.id));
      setMessage(`${user.username} has been deleted.`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "The user could not be deleted.");
    }
  }

  return (
    <div className="adminStack">
      <section className="contentPanel">
        <div className="panelHeading">
          <div>
            <p className="eyebrow">ADMINISTRATION</p>
            <h1>User management</h1>
            <p>Create accounts, update profiles, and control role access.</p>
          </div>
          <button
            className="button buttonSecondary buttonSmall"
            type="button"
            onClick={() => {
              setLoading(true);
              void loadUsers();
            }}
          >
            Refresh
          </button>
        </div>

        <form className="formLayout" onSubmit={submit}>
          <div className="formSectionTitle">
            <h2>{editingId ? "Edit user" : "Create user"}</h2>
            {editingId && <button className="textButton" type="button" onClick={resetForm}>Cancel edit</button>}
          </div>
          <div className="formGrid adminFormGrid">
            <Field label="First name" value={form.first_name} onChange={(value) => setForm({ ...form, first_name: value })} />
            <Field label="Last name" value={form.last_name} onChange={(value) => setForm({ ...form, last_name: value })} />
            <Field label="Email" type="email" value={form.email} onChange={(value) => setForm({ ...form, email: value })} />
            <Field label="Username" value={form.username} onChange={(value) => setForm({ ...form, username: value })} />
            <Field
              label={editingId ? "New password (optional)" : "Password"}
              required={!editingId}
              type="password"
              value={form.password ?? ""}
              onChange={(value) => setForm({ ...form, password: value })}
            />
            <label>
              Role
              <select value={form.role} onChange={(event) => setForm({ ...form, role: event.target.value as UserRole })}>
                <option value="user">User</option>
                <option value="admin">Admin</option>
              </select>
            </label>
          </div>
          <button className="button buttonPrimary" disabled={busy} type="submit">
            {busy ? "Saving…" : editingId ? "Save user" : "Create user"}
          </button>
        </form>

        {message && <p className="formNotice" role="status">{message}</p>}
      </section>

      <section className="contentPanel">
        <div className="tableHeading">
          <h2>All users</h2>
          <span>{users.length} accounts</span>
        </div>
        <div className="usersTable">
          <div className="userRow userHeader">
            <span>User</span><span>Role</span><span>Last online</span><span>Actions</span>
          </div>
          {loading ? (
            <div className="emptyState">Loading users…</div>
          ) : sortedUsers.length > 0 ? (
            sortedUsers.map((user) => (
              <div className="userRow" key={user.id}>
                <div>
                  <b>{user.first_name} {user.last_name}</b>
                  <small>{user.username} · {user.email}</small>
                </div>
                <span><span className={`rolePill ${user.role}`}>{user.role}</span></span>
                <span className="mutedText">
                  {user.last_online ? new Date(user.last_online).toLocaleString() : "Never"}
                </span>
                <div className="rowActions">
                  <button className="textButton" type="button" onClick={() => editUser(user)}>Edit</button>
                  <button
                    className="dangerButton"
                    disabled={user.id === currentUser?.id}
                    type="button"
                    onClick={() => void deleteUser(user)}
                  >
                    Delete
                  </button>
                </div>
              </div>
            ))
          ) : (
            <div className="emptyState">No users found.</div>
          )}
        </div>
      </section>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  type = "text",
  required = true,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
}) {
  return (
    <label>
      {label}
      <input required={required} type={type} value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}