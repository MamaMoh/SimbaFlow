/**
 * User authentication service — communicates with .NET backend /api/auth endpoints.
 */

const API_URL = process.env.BACKEND_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:5117";

/**
 * Sign in against the backend.
 *
 * `clientIp` is the browser's address, passed through so the API's login rate limit can be applied
 * per caller. This call is made server-side, so without it every sign-in on the platform appears to
 * come from this container and would share a single allowance.
 */
export async function authenticate(
  credentials: { username: string; password: string } | FormData,
  clientIp?: string,
) {
  const body = credentials instanceof FormData
    ? { username: credentials.get("username") as string, password: credentials.get("password") as string }
    : credentials;

  const response = await fetch(`${API_URL}/api/auth/login`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...(clientIp ? { "X-Forwarded-For": clientIp } : {}),
    },
    body: JSON.stringify(body),
  });

  const json = await response.json();

  if (!response.ok || !json.isSuccess) {
    // The status travels with the error so the caller can tell the two kinds apart: 401 means the
    // username and password did not match and should stay vague, while 403 means they did match
    // and something else is in the way — a deactivated account, or a suspended agency. That second
    // message is the whole point of the check, so it has to survive as far as the screen.
    const error = new Error(json.error || "Authentication failed") as Error & { status?: number };
    error.status = response.status;
    throw error;
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
    accessToken: data.accessToken as string,
    refreshToken: data.refreshToken as string,
    expiresAt: data.expiresAt as number | string | undefined,
    grantedClaims: (data.grantedClaims ?? data.user?.permissions ?? undefined) as
      | string[]
      | undefined,
    roles: (data.roles ?? data.user?.roles ?? undefined) as string[] | undefined,
    userProfile: (data.userProfile ?? data.user ?? undefined) as unknown,
  };
}
