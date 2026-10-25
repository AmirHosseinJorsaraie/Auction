import { NextRequest, NextResponse } from "next/server";
import { beginLogin, localReturnUrl } from "@/lib/auth/oidc";
import { createSession, OIDC_TRANSACTION_COOKIE } from "@/lib/auth/session-store";

export const runtime = "nodejs";

export async function GET(request: NextRequest): Promise<NextResponse> {
  const fetchSite = request.headers.get("sec-fetch-site");
  if (fetchSite && fetchSite !== "same-origin" && fetchSite !== "none") {
    return NextResponse.json({ error: "cross_site_login_rejected" }, { status: 403 });
  }

  const { transaction, authorizationUrl } = await beginLogin(
    localReturnUrl(request.nextUrl.searchParams.get("returnUrl")),
  );
  const handle = await createSession(
    "public-login",
    transaction,
    new Date(Date.now() + 10 * 60 * 1_000),
  );
  const response = NextResponse.redirect(authorizationUrl);
  response.cookies.set(OIDC_TRANSACTION_COOKIE, handle, {
    httpOnly: true,
    secure: true,
    sameSite: "lax",
    path: "/",
    maxAge: 600,
  });
  return response;
}
