import { afterEach, describe, expect, it, vi } from "vitest";
import { getBffConfig } from "./config";

const valid = {
  NODE_ENV: "production",
  DATABASE_URL: "postgres://user:password@postgres:5432/art_marketplace",
  BFF_SESSION_HMAC_SECRET: "a-strong-random-hmac-value-123456789",
  BFF_SESSION_ENCRYPTION_KEY: Buffer.alloc(32, 7).toString("base64"),
  OIDC_ISSUER: "https://identity.example.com/",
  OIDC_CLIENT_ID: "public-bff",
  OIDC_CLIENT_SECRET: "a-strong-random-client-secret-123456",
  OIDC_CALLBACK_URL: "https://marketplace.example.com/api/auth/callback",
  OIDC_POST_LOGOUT_URL: "https://marketplace.example.com/",
};

function configure(overrides: Partial<typeof valid> = {}) {
  for (const [name, value] of Object.entries({ ...valid, ...overrides })) vi.stubEnv(name, value);
}

afterEach(() => vi.unstubAllEnvs());

describe("production BFF configuration", () => {
  it("accepts strong secrets and HTTPS OIDC endpoints", () => {
    configure();

    expect(getBffConfig().issuer).toBe("https://identity.example.com/");
  });

  it("rejects placeholder secrets", () => {
    configure({ OIDC_CLIENT_SECRET: "replace-me-with-a-long-placeholder-secret" });

    expect(() => getBffConfig()).toThrow(/secret store/);
  });

  it("rejects HTTP authentication endpoints", () => {
    configure({ OIDC_ISSUER: "http://identity.example.com/" });

    expect(() => getBffConfig()).toThrow(/HTTPS/);
  });
});
