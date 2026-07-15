"use server";

import { getServerSession } from "next-auth";
import { authOptions } from "@/lib/auth/authOption";

/**
 * Server actions for user management.
 */

export async function changePassword(data: { currentPassword: string; newPassword: string; confirmPassword?: string }) {
  const API_URL = process.env.BACKEND_URL || process.env.API_URL || "http://localhost:5117";

  // Get the session to extract the access token
  const session = await getServerSession(authOptions);
  const accessToken = (session?.user as any)?.accessToken;

  if (!accessToken) {
    return { success: false, error: "Not authenticated. Please login again." };
  }

  const response = await fetch(`${API_URL}/api/auth/change-password`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "Authorization": `Bearer ${accessToken}`,
    },
    body: JSON.stringify(data),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ error: "Password change failed" }));
    return { success: false, error: error.error || "Password change failed" };
  }

  return { success: true };
}
