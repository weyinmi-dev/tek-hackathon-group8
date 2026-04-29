"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { rankOf, type Role } from "@/lib/rbac";

/**
 * Client-side guard: redirects to /dashboard when the signed-in user is below
 * <minRole>. The backend still rejects the requests these pages make, but the
 * redirect saves them from a wall of 403s on a page they shouldn't have opened.
 */
export function RoleGate({ minRole, children }: { minRole: Role; children: React.ReactNode }) {
  const router = useRouter();
  const { user, ready } = useAuth();

  useEffect(() => {
    if (!ready) return;
    if (!user) { router.push("/login"); return; }
    if (rankOf(user.role) < rankOf(minRole)) {
      router.push("/dashboard");
    }
  }, [ready, user, router, minRole]);

  if (!ready || !user) return null;
  if (rankOf(user.role) < rankOf(minRole)) return null;
  return <>{children}</>;
}
