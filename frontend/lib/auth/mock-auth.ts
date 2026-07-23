export const MOCK_ACCESS_TOKEN = "mock-access-token";
export const MOCK_REFRESH_TOKEN = "mock-refresh-token";

export const MOCK_GRANTED_CLAIMS = [
  "candidate.read",
  "candidate.create",
  "workflow.view",
  "embassy.read",
  "lmis.read",
  "travel.read",
  "arrival.read",
  "commission.read",
  "accounting.read",
  "report.view",
  "staff.read",
  "role.read",
  "office.read",
  "partner.read",
  "workflow.configure",
  "tenant.manage",
  "system.admin",
] as const;

export function isMockAuthEnabled() {
  return (
    process.env.NEXT_PUBLIC_USE_MOCKS === "true" ||
    process.env.NEXT_PUBLIC_USE_MOCKS === "1"
  );
}

export function buildMockAuthUser(username = "demo") {
  return {
    id: "mock-user",
    accessToken: MOCK_ACCESS_TOKEN,
    refreshToken: MOCK_REFRESH_TOKEN,
    expiresAt: Date.now() + 7 * 24 * 60 * 60 * 1000,
    userProfile: {
      userId: "mock-user",
      username,
      fullName: "Demo User",
      email: "demo@simbaflow.local",
      isFirstLogin: false,
    },
    grantedClaims: [...MOCK_GRANTED_CLAIMS],
    roles: ["Admin"],
    requiresPasswordChange: false,
    requiresMfa: false,
  };
}
