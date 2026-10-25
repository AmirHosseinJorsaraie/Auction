import { createHash, randomBytes, timingSafeEqual } from "node:crypto";
import { createRemoteJWKSet, jwtVerify, type JWTPayload } from "jose";
import { getBffConfig } from "./config";

type Discovery = {
  authorization_endpoint: string;
  token_endpoint: string;
  jwks_uri: string;
  end_session_endpoint?: string;
};

export type LoginTransaction = {
  state: string;
  nonce: string;
  verifier: string;
  returnUrl: string;
};

export type PublicSession = {
  accessToken: string;
  refreshToken?: string;
  idToken: string;
  accessTokenExpiresAt: number;
  sessionExpiresAt: number;
  csrfToken: string;
  claims: JWTPayload;
};

let discoveryPromise: Promise<Discovery> | undefined;

export function secureEqual(left: string, right: string): boolean {
  const a = Buffer.from(left);
  const b = Buffer.from(right);
  return a.length === b.length && timingSafeEqual(a, b);
}

export function localReturnUrl(value: string | null): string {
  return value?.startsWith("/") && !value.startsWith("//") ? value : "/";
}

export async function beginLogin(returnUrl: string): Promise<{
  transaction: LoginTransaction;
  authorizationUrl: URL;
}> {
  const config = getBffConfig();
  const metadata = await discovery();
  const transaction = {
    state: randomBytes(32).toString("base64url"),
    nonce: randomBytes(32).toString("base64url"),
    verifier: randomBytes(48).toString("base64url"),
    returnUrl,
  };
  const challenge = createHash("sha256")
    .update(transaction.verifier, "ascii")
    .digest("base64url");
  const authorizationUrl = new URL(metadata.authorization_endpoint);
  authorizationUrl.search = new URLSearchParams({
    client_id: config.clientId,
    redirect_uri: config.callbackUrl,
    response_type: "code",
    scope: "openid profile email roles art-marketplace-api offline_access",
    code_challenge: challenge,
    code_challenge_method: "S256",
    state: transaction.state,
    nonce: transaction.nonce,
  }).toString();
  return { transaction, authorizationUrl };
}

export async function redeemCode(
  code: string,
  transaction: LoginTransaction,
): Promise<PublicSession> {
  const config = getBffConfig();
  const metadata = await discovery();
  const response = await fetch(metadata.token_endpoint, {
    method: "POST",
    headers: {
      authorization: `Basic ${Buffer.from(`${config.clientId}:${config.clientSecret}`).toString("base64")}`,
      "content-type": "application/x-www-form-urlencoded",
    },
    body: new URLSearchParams({
      grant_type: "authorization_code",
      code,
      redirect_uri: config.callbackUrl,
      code_verifier: transaction.verifier,
    }),
    cache: "no-store",
  });
  const tokens = (await response.json()) as Record<string, unknown>;
  if (!response.ok || typeof tokens.id_token !== "string" || typeof tokens.access_token !== "string") {
    throw new Error("OIDC token exchange failed");
  }

  const verified = await jwtVerify(
    tokens.id_token,
    createRemoteJWKSet(new URL(metadata.jwks_uri)),
    { issuer: config.issuer, audience: config.clientId },
  );
  if (verified.payload.nonce !== transaction.nonce) {
    throw new Error("OIDC nonce validation failed");
  }

  const expiresIn = typeof tokens.expires_in === "number" ? tokens.expires_in : 600;
  return {
    accessToken: tokens.access_token,
    refreshToken: typeof tokens.refresh_token === "string" ? tokens.refresh_token : undefined,
    idToken: tokens.id_token,
    accessTokenExpiresAt: Date.now() + expiresIn * 1_000,
    sessionExpiresAt: Date.now() + 30 * 60 * 1_000,
    csrfToken: randomBytes(32).toString("base64url"),
    claims: verified.payload,
  };
}

export async function refreshTokens(session: PublicSession): Promise<PublicSession> {
  if (!session.refreshToken) throw new Error("No refresh token is available");
  const config = getBffConfig();
  const metadata = await discovery();
  const response = await fetch(metadata.token_endpoint, {
    method: "POST",
    headers: {
      authorization: `Basic ${Buffer.from(`${config.clientId}:${config.clientSecret}`).toString("base64")}`,
      "content-type": "application/x-www-form-urlencoded",
    },
    body: new URLSearchParams({
      grant_type: "refresh_token",
      refresh_token: session.refreshToken,
    }),
    cache: "no-store",
  });
  const tokens = (await response.json()) as Record<string, unknown>;
  if (!response.ok || typeof tokens.access_token !== "string") {
    throw new Error("OIDC refresh failed");
  }
  return {
    ...session,
    accessToken: tokens.access_token,
    refreshToken: typeof tokens.refresh_token === "string" ? tokens.refresh_token : session.refreshToken,
    idToken: typeof tokens.id_token === "string" ? tokens.id_token : session.idToken,
    accessTokenExpiresAt:
      Date.now() + (typeof tokens.expires_in === "number" ? tokens.expires_in : 600) * 1_000,
  };
}

export async function endSessionUrl(idToken: string): Promise<URL> {
  const config = getBffConfig();
  const metadata = await discovery();
  const target = new URL(metadata.end_session_endpoint ?? config.postLogoutUrl);
  if (metadata.end_session_endpoint) {
    target.searchParams.set("id_token_hint", idToken);
    target.searchParams.set("post_logout_redirect_uri", config.postLogoutUrl);
  }
  return target;
}

async function discovery(): Promise<Discovery> {
  discoveryPromise ??= fetch(
    new URL(".well-known/openid-configuration", getBffConfig().issuer),
    { cache: "force-cache" },
  ).then(async (response) => {
    if (!response.ok) throw new Error("OIDC discovery failed");
    return (await response.json()) as Discovery;
  });
  return discoveryPromise;
}
