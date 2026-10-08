"use client";

import Link from "next/link";
import { ProtectedPage } from "@/components/protected-page";
import { useAuth } from "@/components/auth-provider";

// Guards the dashboard before rendering its authenticated workspace.
export default function DashboardPage() {
  return (
    <ProtectedPage>
      <DashboardContent />
    </ProtectedPage>
  );
}

// Renders standard destinations plus administrator-only management cards.
function DashboardContent() {
  const { user } = useAuth();
  if (!user) return null;

  const fullName = `${user.first_name} ${user.last_name}`.trim();

  return (
    <main className="appPage">
      <div className="pageContainer">
        <section className="welcomePanel">
          <div>
            <p className="eyebrow">YOUR WORKSPACE</p>
            <h1>
              Welcome <span>{fullName}</span> as <span>{user.role}</span>
            </h1>
            <p>Your ABADAR account is ready. Choose where you would like to begin.</p>
          </div>
          <span className={`rolePill ${user.role}`}>{user.role} account</span>
        </section>

        <section className="dashboardGrid">
          <Link className="dashboardCard" href="/markets">
            <span className="cardIcon">M</span>
            <div>
              <h2>Markets</h2>
              <p>View the latest simulated prices, changes, and market status.</p>
            </div>
            <span className="cardArrow">→</span>
          </Link>

          <Link className="dashboardCard" href="/account">
            <span className="cardIcon">A</span>
            <div>
              <h2>Account</h2>
              <p>Review and update your profile and sign-in information.</p>
            </div>
            <span className="cardArrow">→</span>
          </Link>

          {user.role === "admin" && (
            <>
              <Link className="dashboardCard adminCard" href="/admin/users">
                <span className="cardIcon">U</span>
                <div>
                  <h2>User administration</h2>
                  <p>Create accounts, assign roles, and manage platform access.</p>
                </div>
                <span className="cardArrow">→</span>
              </Link>
              <Link className="dashboardCard adminCard" href="/admin/simulator">
                <span className="cardIcon">S</span>
                <div>
                  <h2>Market simulator</h2>
                  <p>Generate orders, inspect durable history, and reset test data.</p>
                </div>
                <span className="cardArrow">→</span>
              </Link>
            </>
          )}
        </section>
      </div>
    </main>
  );
}