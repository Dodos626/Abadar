"use client";

import { useEffect, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/auth-provider";
import type { UserRole } from "@/lib/api";

export function ProtectedPage({
  children,
  roles,
}: {
  children: ReactNode;
  roles?: UserRole[];
}) {
  const { status, user } = useAuth();
  const pathname = usePathname();
  const router = useRouter();
  const roleAllowed = Boolean(user && (!roles || roles.includes(user.role)));

  useEffect(() => {
    if (status === "anonymous") {
      router.replace(`/login?next=${encodeURIComponent(pathname)}`);
      return;
    }

    if (status === "authenticated" && !roleAllowed) {
      router.replace("/not-authorised");
    }
  }, [pathname, roleAllowed, router, status]);

  if (status === "loading" || status === "anonymous" || !roleAllowed) {
    return (
      <main className="centeredPage" aria-live="polite">
        <div className="loader" />
        <p>Checking access…</p>
      </main>
    );
  }

  return children;
}