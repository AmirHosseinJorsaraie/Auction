import { NextRequest, NextResponse } from "next/server";
import { endSessionUrl, secureEqual, type PublicSession } from "@/lib/auth/oidc";
import { getSession, PUBLIC_SESSION_COOKIE, revokeSession } from "@/lib/auth/session-store";

export const runtime = "nodejs";

export async function POST(request: NextRequest): Promise<NextResponse> {
  const handle = request.cookies.get(PUBLIC_SESSION_COOKIE)?.value;
  const session = await getSession<PublicSession>(handle, "public");
  const csrf = request.headers.get("x-csrf-token");
  const origin = request.headers.get("origin");
  if (!session || !csrf || !secureEqual(csrf, session.csrfToken) || origin !== request.nextUrl.origin) {
    return NextResponse.json({ error: "invalid_csrf" }, { status: 403 });
  }
  await revokeSession(handle, "public");
  const response = NextResponse.redirect(await endSessionUrl(session.idToken), 303);
  response.cookies.delete(PUBLIC_SESSION_COOKIE);
  return response;
}
