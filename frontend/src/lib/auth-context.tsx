"use client";

import { createContext, useCallback, useContext, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { authApi, getLastRefreshedUser, tryRefresh } from "./api";
import type { UserDto } from "./types";

interface AuthContextValue {
  user: UserDto | null;
  status: "loading" | "authenticated" | "unauthenticated";
  setUser: (user: UserDto | null) => void;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Bootstraps the session on mount: exchanges the httpOnly refresh cookie for an
 * in-memory access token. Children render only once authenticated.
 */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(null);
  const [status, setStatus] = useState<AuthContextValue["status"]>("loading");
  const router = useRouter();

  useEffect(() => {
    let cancelled = false;
    (async () => {
      const ok = await tryRefresh();
      if (cancelled) return;
      if (ok) {
        setUser(getLastRefreshedUser());
        setStatus("authenticated");
      } else {
        setStatus("unauthenticated");
        router.replace("/giris");
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [router]);

  const logout = useCallback(async () => {
    await authApi.logout();
    setUser(null);
    setStatus("unauthenticated");
    router.replace("/giris");
  }, [router]);

  return (
    <AuthContext.Provider value={{ user, status, setUser, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside AuthProvider");
  }
  return context;
}
