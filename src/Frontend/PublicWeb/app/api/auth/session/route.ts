import { NextRequest, NextResponse } from "next/server";
import type { PublicSession } from "@/lib/auth/oidc";
import { getSession, PUBLIC_SESSION_COOKIE } from "@/lib/auth/session-store";

export const runtime = "nodejs";

export async function GET(request: NextRequest): Promise<NextResponse> {
  const session = await getSession<PublicSession>(
    request.cookies.get(PUBLIC_SESSION_COOKIE)?.value,
    "public",
  );
  if (!session) return NextResponse.json({ authenticated: false }, { status: 401 });
  return NextResponse.json({
    authenticated: true,
    user: {
      sub: session.claims.sub,
      name: session.claims.name,
      email: session.claims.email,
      roles: session.claims.role,
    },
    csrfToken: session.csrfToken,
  });
}
