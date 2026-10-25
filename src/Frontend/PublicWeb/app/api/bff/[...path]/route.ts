import { NextRequest, NextResponse } from "next/server";
import { refreshTokens, secureEqual, type PublicSession } from "@/lib/auth/oidc";
import {
  getSession,
  PUBLIC_SESSION_COOKIE,
  revokeSession,
  updateSession,
} from "@/lib/auth/session-store";

export const runtime = "nodejs";

type RouteContext = { params: Promise<{ path: string[] }> };

async function proxy(request: NextRequest, context: RouteContext): Promise<NextResponse> {
  const handle = request.cookies.get(PUBLIC_SESSION_COOKIE)?.value;
  let session = await getSession<PublicSession>(handle, "public");
  if (!handle || !session) {
    return NextResponse.json({ error: "unauthenticated" }, { status: 401 });
  }

  if (!["GET", "HEAD", "OPTIONS"].includes(request.method)) {
    const csrf = request.headers.get("x-csrf-token");
    const origin = request.headers.get("origin");
    if (!csrf || !secureEqual(csrf, session.csrfToken) || origin !== request.nextUrl.origin) {
      return NextResponse.json({ error: "invalid_csrf" }, { status: 403 });
    }
  }

  if (session.accessTokenExpiresAt <= Date.now() + 30_000) {
    try {
      session = await refreshTokens(session);
      if (!(await updateSession(handle, session, new Date(session.sessionExpiresAt)))) {
        throw new Error("Session update was rejected");
      }
    } catch {
      await revokeSession(handle, "public");
      const response = NextResponse.json({ error: "session_expired" }, { status: 401 });
      response.cookies.delete(PUBLIC_SESSION_COOKIE);
      return response;
    }
  }

  const { path } = await context.params;
  const apiBase = process.env.API_BASE_URL;
  if (!apiBase) throw new Error("API_BASE_URL is required");
  const target = new URL(`/api/${path.map(encodeURIComponent).join("/")}`, apiBase);
  target.search = request.nextUrl.search;

  const headers = new Headers();
  headers.set("authorization", `Bearer ${session.accessToken}`);
  headers.set("accept", request.headers.get("accept") ?? "application/json");
  const contentType = request.headers.get("content-type");
  if (contentType) headers.set("content-type", contentType);
  const traceparent = request.headers.get("traceparent");
  if (traceparent) headers.set("traceparent", traceparent);

  const upstream = await fetch(target, {
    method: request.method,
    headers,
    body: ["GET", "HEAD"].includes(request.method) ? undefined : await request.arrayBuffer(),
    redirect: "manual",
    cache: "no-store",
  });
  const responseHeaders = new Headers();
  const upstreamContentType = upstream.headers.get("content-type");
  if (upstreamContentType) responseHeaders.set("content-type", upstreamContentType);
  return new NextResponse(upstream.body, {
    status: upstream.status,
    headers: responseHeaders,
  });
}

export const GET = proxy;
export const POST = proxy;
export const PUT = proxy;
export const PATCH = proxy;
export const DELETE = proxy;
