/**
 * Session cache to reduce repeated getServerSession calls
 * Caches session for a short duration to improve performance
 */

import { getServerSession } from "next-auth";
import { authOptions } from "@/lib/auth/authOption";
import type { Session } from "next-auth";

// Cache session for 5 seconds to reduce repeated calls
const CACHE_TTL = 5000; // 5 seconds

interface CachedSession {
  session: Session | null;
  timestamp: number;
}

let sessionCache: CachedSession | null = null;

/**
 * Get server session with caching
 * Reduces repeated calls to getServerSession which can be slow
 */
export async function getCachedServerSession(): Promise<Session | null> {
  const now = Date.now();
  
  // Return cached session if still valid
  if (sessionCache && (now - sessionCache.timestamp) < CACHE_TTL) {
    return sessionCache.session;
  }
  
  // Fetch fresh session
  const session = await getServerSession(authOptions);
  
  // Update cache
  sessionCache = {
    session,
    timestamp: now,
  };
  
  return session;
}

/**
 * Clear session cache (useful after mutations)
 */
export function clearSessionCache(): void {
  sessionCache = null;
}

