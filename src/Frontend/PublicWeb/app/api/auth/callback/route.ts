import { NextRequest, NextResponse } from "next/server";
import { redeemCode, secureEqual, type LoginTransaction } from "@/lib/auth/oidc";
import {
  createSession,
  getSession,
  OIDC_TRANSACTION_COOKIE,
  PUBLIC_SESSION_COOKIE,
} from "@/lib/auth/session-store";

export const runtime = "nodejs";

export async function GET(request: NextRequest): Promise<NextResponse> {
  const code = request.nextUrl.searchParams.get("code");
  const state = request.nextUrl.searchParams.get("state");
  const transactionHandle = request.cookies.get(OIDC_TRANSACTION_COOKIE)?.value;
  const transaction = await getSession<LoginTransaction>(transactionHandle, "public-login", true);
  if (!code || !state || !transaction || !secureEqual(state, transaction.state)) {
    return NextResponse.json({ error: "invalid_oidc_callback" }, { status: 400 });
  }

  const session = await redeemCode(code, transaction);
  const sessionHandle = await createSession(
    "public",
    session,
    new Date(session.sessionExpiresAt),
    typeof session.claims.sub === "string" ? session.claims.sub : undefined,
  );
  const response = NextResponse.redirect(new URL(transaction.returnUrl, request.nextUrl.origin));
  response.cookies.delete(OIDC_TRANSACTION_COOKIE);
  response.cookies.set(PUBLIC_SESSION_COOKIE, sessionHandle, {
    httpOnly: true,
    secure: true,
    sameSite: "lax",
    path: "/",
    maxAge: 30 * 60,
  });
  return response;
}
