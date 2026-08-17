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

  try {
    // Backend expects: CurrentPassword, NewPassword, ConfirmPassword (PascalCase)
    const payload = {
      currentPassword: data.currentPassword,
      newPassword: data.newPassword,
      confirmPassword: data.confirmPassword || data.newPassword,
    };

    const response = await fetch(`${API_URL}/api/auth/change-password`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${accessToken}`,
      },
      body: JSON.stringify(payload),
    });

    const result = await response.json().catch(() => null);

    if (!response.ok || (result && !result.isSuccess)) {
      // Extract the actual error message from the backend
      const errorMessage = result?.error 
        || result?.errors?.join("; ")
        || (result?.data && typeof result.data === 'string' ? result.data : null)
        || `Password change failed (HTTP ${response.status})`;
      return { success: false, error: errorMessage };
    }

    return { success: true };
  } catch (err: any) {
    return { success: false, error: err?.message || "Network error. Please try again." };
  }
}
