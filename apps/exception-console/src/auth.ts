/**
 * Entra ID (Azure AD) auth config. Roles: Dispatcher / Warehouse Manager / Admin — defined as
 * app roles on the Entra app registration and surfaced in the token `roles` claim.
 *
 * This module exposes a minimal shape so the app can run in dev without MSAL wired. In production,
 * install @azure/msal-browser + @azure/msal-react, acquire a token for the API scope, and pass a
 * tokenFactory to api/signalr. Gate the action buttons on the user's role claim.
 */
export const authConfig = {
  clientId: import.meta.env.VITE_ENTRA_CLIENT_ID ?? "<app-registration-client-id>",
  authority: `https://login.microsoftonline.com/${import.meta.env.VITE_ENTRA_TENANT_ID ?? "<tenant-id>"}`,
  apiScope: import.meta.env.VITE_API_SCOPE ?? "api://tracktrash-tracking/.default",
};

export type Role = "Dispatcher" | "WarehouseManager" | "Admin";

export interface CurrentUser {
  name: string;
  upn: string;
  roles: Role[];
  getToken: () => string | undefined;
}

/** Dev stub. Replace with MSAL-derived user in production. */
export function useDevUser(): CurrentUser {
  return {
    name: "Dev User",
    upn: "dev@a-squaretechnologies.com",
    roles: ["Admin"],
    getToken: () => undefined,
  };
}

export function canResolve(user: CurrentUser): boolean {
  return user.roles.includes("Admin") || user.roles.includes("WarehouseManager");
}
export function canEscalate(user: CurrentUser): boolean {
  return user.roles.length > 0; // any authenticated role can escalate
}
