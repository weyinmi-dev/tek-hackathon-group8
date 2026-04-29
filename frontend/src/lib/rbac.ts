// Frontend RBAC helpers — these guard the UI for ergonomics.
// The backend enforces the same rules on every endpoint, so a determined user
// who edits cookies still cannot escalate; this just hides controls they
// would not be able to use anyway.

import type { AuthUser } from "./types";

export type Role = AuthUser["role"];

const RANK: Record<Role, number> = { viewer: 0, engineer: 1, manager: 2, admin: 3 };

export function rankOf(role: Role | string | undefined): number {
  return RANK[(role as Role) ?? "viewer"] ?? 0;
}

export const isEngineer = (role: Role | string | undefined) => rankOf(role) >= RANK.engineer;
export const isManager  = (role: Role | string | undefined) => rankOf(role) >= RANK.manager;
export const isAdmin    = (role: Role | string | undefined) => rankOf(role) >= RANK.admin;

/**
 * Manager-tier rule from the backend: managers cannot create or modify Admin
 * accounts. Mirrors `UserErrors.CannotManageAdmin`.
 */
export function canManageTarget(actorRole: Role | string | undefined, targetRole: Role | string | undefined): boolean {
  if (isAdmin(actorRole)) return true;
  if (isManager(actorRole)) return targetRole !== "admin";
  return false;
}
