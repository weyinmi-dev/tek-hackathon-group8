"use client";

import { useEffect, useState } from "react";
import type { AuthUser, LoginResponse } from "./types";
import { api } from "./api";

const ACCESS_KEY  = "tp_access";
const REFRESH_KEY = "tp_refresh";
const USER_KEY    = "tp_user";
const COOKIE      = "tp_access";

export function persistSession(login: LoginResponse) {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(ACCESS_KEY, login.accessToken);
  window.localStorage.setItem(REFRESH_KEY, login.refreshToken);
  window.localStorage.setItem(USER_KEY, JSON.stringify(login.user));
  // Expose to server for redirect logic in /page.tsx via a non-HttpOnly cookie.
  // Real production use HttpOnly + secure on server-issued cookies; this is a demo.
  document.cookie = `${COOKIE}=${login.accessToken}; Path=/; SameSite=Lax`;
}

export function clearSession() {
  if (typeof window === "undefined") return;
  window.localStorage.removeItem(ACCESS_KEY);
  window.localStorage.removeItem(REFRESH_KEY);
  window.localStorage.removeItem(USER_KEY);
  document.cookie = `${COOKIE}=; Path=/; Max-Age=0; SameSite=Lax`;
}

export function readUser(): AuthUser | null {
  if (typeof window === "undefined") return null;
  try { return JSON.parse(window.localStorage.getItem(USER_KEY) || "null"); }
  catch { return null; }
}

export function useAuth() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    setUser(readUser());
    setReady(true);
  }, []);

  async function logout() {
    clearSession();
    setUser(null);
    if (typeof window !== "undefined") window.location.href = "/login";
  }

  async function login(email: string, password: string) {
    const r = await api.login(email, password);
    persistSession(r);
    setUser(r.user);
    return r;
  }

  return { user, ready, login, logout };
}
