export type BffConfig = {
  databaseUrl: string;
  hmacSecret: Buffer;
  encryptionKey: Buffer;
  issuer: string;
  clientId: string;
  clientSecret: string;
  callbackUrl: string;
  postLogoutUrl: string;
};

function required(name: string): string {
  const value = process.env[name];
  if (!value) {
    throw new Error(`${name} is required`);
  }
  return value;
}

function rejectProductionPlaceholder(name: string, value: string): void {
  if (process.env.NODE_ENV !== "production") return;
  const normalized = value.toLowerCase();
  if (value.length < 32 || ["change-me", "replace-me", "development-only", "example.invalid"].some((item) => normalized.includes(item))) {
    throw new Error(`${name} must be supplied by the production secret store and cannot contain a placeholder`);
  }
}

function productionHttpsUrl(name: string): string {
  const value = required(name);
  const url = new URL(value);
  if (process.env.NODE_ENV === "production"
    && (url.protocol !== "https:" || url.hostname.endsWith(".invalid"))) {
    throw new Error(`${name} must use an approved HTTPS endpoint in production`);
  }
  return url.toString();
}

export function getBffConfig(): BffConfig {
  const encodedEncryptionKey = required("BFF_SESSION_ENCRYPTION_KEY");
  const encryptionKey = Buffer.from(encodedEncryptionKey, "base64");
  if (encryptionKey.length !== 32) {
    throw new Error("BFF_SESSION_ENCRYPTION_KEY must be a base64-encoded 32-byte key");
  }

  const hmacSecret = Buffer.from(required("BFF_SESSION_HMAC_SECRET"), "utf8");
  if (hmacSecret.length < 32) {
    throw new Error("BFF_SESSION_HMAC_SECRET must contain at least 32 bytes");
  }
  const clientSecret = required("OIDC_CLIENT_SECRET");
  rejectProductionPlaceholder("OIDC_CLIENT_SECRET", clientSecret);
  rejectProductionPlaceholder("BFF_SESSION_HMAC_SECRET", hmacSecret.toString("utf8"));
  rejectProductionPlaceholder("BFF_SESSION_ENCRYPTION_KEY", encodedEncryptionKey);

  return {
    databaseUrl: required("DATABASE_URL"),
    hmacSecret,
    encryptionKey,
    issuer: productionHttpsUrl("OIDC_ISSUER"),
    clientId: required("OIDC_CLIENT_ID"),
    clientSecret,
    callbackUrl: productionHttpsUrl("OIDC_CALLBACK_URL"),
    postLogoutUrl: productionHttpsUrl("OIDC_POST_LOGOUT_URL"),
  };
}
