import {
  createCipheriv,
  createDecipheriv,
  createHmac,
  randomBytes,
  randomUUID,
} from "node:crypto";
import { Pool } from "pg";
import { getBffConfig } from "./config";

export const PUBLIC_SESSION_COOKIE = "__Host-artmarketplace.public";
export const OIDC_TRANSACTION_COOKIE = "__Host-artmarketplace.public.oidc";

let pool: Pool | undefined;

function database(): Pool {
  pool ??= new Pool({
    connectionString: getBffConfig().databaseUrl,
    max: 10,
    idleTimeoutMillis: 30_000,
    connectionTimeoutMillis: 5_000,
  });
  return pool;
}

function digest(handle: string): string {
  return createHmac("sha256", getBffConfig().hmacSecret)
    .update(handle, "utf8")
    .digest("hex")
    .toUpperCase();
}

function protect(value: unknown): Buffer {
  const nonce = randomBytes(12);
  const cipher = createCipheriv("aes-256-gcm", getBffConfig().encryptionKey, nonce);
  const ciphertext = Buffer.concat([
    cipher.update(JSON.stringify(value), "utf8"),
    cipher.final(),
  ]);
  return Buffer.concat([nonce, cipher.getAuthTag(), ciphertext]);
}

function unprotect<T>(value: Buffer): T {
  if (value.length < 29) {
    throw new Error("Invalid protected session payload");
  }
  const decipher = createDecipheriv(
    "aes-256-gcm",
    getBffConfig().encryptionKey,
    value.subarray(0, 12),
  );
  decipher.setAuthTag(value.subarray(12, 28));
  return JSON.parse(
    Buffer.concat([decipher.update(value.subarray(28)), decipher.final()]).toString("utf8"),
  ) as T;
}

export async function createSession<T>(
  surface: "public" | "public-login",
  payload: T,
  expiresAt: Date,
): Promise<string> {
  const handle = randomBytes(32).toString("base64url");
  await database().query(
    `INSERT INTO identity.bff_sessions
      ("Id", "Surface", "HandleDigest", "ProtectedTicket", "CreatedAtUtc", "ExpiresAtUtc", "Version")
     VALUES ($1, $2, $3, $4, NOW(), $5, 1)`,
    [randomUUID(), surface, digest(handle), protect(payload), expiresAt],
  );
  return handle;
}

export async function getSession<T>(
  handle: string | undefined,
  surface: "public" | "public-login",
  consume = false,
): Promise<T | null> {
  if (!handle) return null;
  const client = await database().connect();
  try {
    await client.query("BEGIN");
    const result = await client.query<{ ProtectedTicket: Buffer }>(
      `SELECT "ProtectedTicket" FROM identity.bff_sessions
       WHERE "Surface" = $1 AND "HandleDigest" = $2
         AND "RevokedAtUtc" IS NULL AND "ExpiresAtUtc" > NOW()
       FOR UPDATE`,
      [surface, digest(handle)],
    );
    if (result.rowCount !== 1) {
      await client.query("ROLLBACK");
      return null;
    }
    if (consume) {
      await client.query(
        `UPDATE identity.bff_sessions
         SET "RevokedAtUtc" = NOW(), "Version" = "Version" + 1
         WHERE "Surface" = $1 AND "HandleDigest" = $2`,
        [surface, digest(handle)],
      );
    }
    await client.query("COMMIT");
    return unprotect<T>(result.rows[0].ProtectedTicket);
  } catch (error) {
    await client.query("ROLLBACK");
    await revokeSession(handle, surface);
    throw error;
  } finally {
    client.release();
  }
}

export async function updateSession<T>(
  handle: string,
  payload: T,
  expiresAt: Date,
): Promise<boolean> {
  const result = await database().query(
    `UPDATE identity.bff_sessions
     SET "ProtectedTicket" = $1, "ExpiresAtUtc" = $2, "Version" = "Version" + 1
     WHERE "Surface" = 'public' AND "HandleDigest" = $3
       AND "RevokedAtUtc" IS NULL AND "ExpiresAtUtc" > NOW()`,
    [protect(payload), expiresAt, digest(handle)],
  );
  return result.rowCount === 1;
}

export async function revokeSession(
  handle: string | undefined,
  surface: "public" | "public-login",
): Promise<void> {
  if (!handle) return;
  await database().query(
    `UPDATE identity.bff_sessions
     SET "RevokedAtUtc" = COALESCE("RevokedAtUtc", NOW()), "Version" = "Version" + 1
     WHERE "Surface" = $1 AND "HandleDigest" = $2 AND "RevokedAtUtc" IS NULL`,
    [surface, digest(handle)],
  );
}
