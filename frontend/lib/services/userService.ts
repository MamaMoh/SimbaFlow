/**
 * User authentication service — communicates with .NET backend /api/auth endpoints.
 */

const API_URL = process.env.BACKEND_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5117";

export async function authenticate(credentials: { username: string; password: string } | FormData) {
  const body = credentials instanceof FormData
    ? { username: credentials.get("username") as string, password: credentials.get("password") as string }
    : credentials;

  const response = await fetch(`${API_URL}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  const json = await response.json();

  if (!response.ok || !json.isSuccess) {
    throw new Error(json.error || "Authentication failed");
  }

  // Unwrap the Result<T>.data envelope to match what authOption.ts expects
  const data = json.data;
  return {
    accessToken: data.accessToken,
    refreshToken: data.refreshToken,
    expiresAt: data.expiresAt,
    requiresPasswordChange: data.requiresPasswordChange || false,
    requiresMfa: data.requiresMfa || false,
    userProfile: {
      userId: data.user?.id,
      username: data.user?.username,
      fullName: data.user?.fullName,
      email: data.user?.email,
      phoneNumber: data.user?.phoneNumber,
      profileImageUrl: data.user?.profileImageUrl,
      isFirstLogin: data.user?.isFirstLogin,
      isSuperAdmin: data.user?.isSuperAdmin,
      departmentId: data.user?.departmentId,
    },
    grantedClaims: data.user?.permissions || [],
    roles: data.user?.roles || [],
  };
}

export async function refreshAccessToken(refreshToken: string) {
  const response = await fetch(`${API_URL}/api/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });

  const json = await response.json();

  if (!response.ok || !json.isSuccess) {
    throw new Error("Token refresh failed");
  }

  const data = json.data;
  return {
    accessToken: data.accessToken,
    refreshToken: data.refreshToken,
    expiresAt: data.expiresAt,
  };
}
