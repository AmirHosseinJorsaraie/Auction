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

export function getBffConfig(): BffConfig {
  const encryptionKey = Buffer.from(required("BFF_SESSION_ENCRYPTION_KEY"), "base64");
  if (encryptionKey.length !== 32) {
    throw new Error("BFF_SESSION_ENCRYPTION_KEY must be a base64-encoded 32-byte key");
  }

  const hmacSecret = Buffer.from(required("BFF_SESSION_HMAC_SECRET"), "utf8");
  if (hmacSecret.length < 32) {
    throw new Error("BFF_SESSION_HMAC_SECRET must contain at least 32 bytes");
  }

  return {
    databaseUrl: required("DATABASE_URL"),
    hmacSecret,
    encryptionKey,
    issuer: new URL(required("OIDC_ISSUER")).toString(),
    clientId: required("OIDC_CLIENT_ID"),
    clientSecret: required("OIDC_CLIENT_SECRET"),
    callbackUrl: new URL(required("OIDC_CALLBACK_URL")).toString(),
    postLogoutUrl: new URL(required("OIDC_POST_LOGOUT_URL")).toString(),
  };
}
