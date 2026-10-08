"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth-provider";

type NavItem = {
  href: string;
  label: string;
};

const publicNavigation: NavItem[] = [
  { href: "/", label: "Home" },
  { href: "/#features", label: "Features" },
  { href: "/#platform", label: "Platform" },
];

const userNavigation: NavItem[] = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/markets", label: "Markets" },
  { href: "/account", label: "Account" },
];

const adminNavigation: NavItem[] = [
  { href: "/dashboard", label: "Dashboard" },
  { href: "/admin/users", label: "Users" },
  { href: "/admin/simulator", label: "Simulator" },
  { href: "/markets", label: "Markets" },
  { href: "/account", label: "Account" },
];

// Renders role-aware navigation including the administrator simulator workspace.
export function SiteHeader() {
  const { status, user, logout } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const navigation =
    status === "authenticated"
      ? user?.role === "admin"
        ? adminNavigation
        : userNavigation
      : publicNavigation;

  // Clears the browser session and returns the user to the landing page.
  function signOut() {
    logout();
    router.push("/");
  }

  return (
    <header className="siteHeader">
      <div className="navContainer">
        <Link className="brand" href="/" aria-label="ABADAR home">
          <span className="brandMark">A</span>
          <span className="brandText">ABADAR</span>
        </Link>

        <nav className="navLinks" aria-label="Primary navigation">
          {navigation.map((item) => {
            const active =
              item.href !== "/" && !item.href.includes("#")
                ? pathname.startsWith(item.href)
                : pathname === item.href;

            return (
              <Link className={active ? "active" : ""} href={item.href} key={item.href}>
                {item.label}
              </Link>
            );
          })}
        </nav>

        <div className="navActions">
          {status === "loading" ? (
            <span className="navPlaceholder" aria-label="Loading session" />
          ) : status === "authenticated" && user ? (
            <>
              <span className={`rolePill ${user.role}`}>{user.role}</span>
              <button className="button buttonGhost buttonSmall" type="button" onClick={signOut}>
                Logout
              </button>
            </>
          ) : (
            <Link className="button buttonPrimary buttonSmall" href="/login">
              Login
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}