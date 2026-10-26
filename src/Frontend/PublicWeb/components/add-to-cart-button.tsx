"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";

export function AddToCartButton({ listingId }: { listingId: string }) {
  const router = useRouter();
  const [message, setMessage] = useState(""); const [busy, setBusy] = useState(false);
  async function add() {
    setBusy(true); setMessage("");
    try {
      const session = await fetch("/api/auth/session", { cache: "no-store" });
      if (session.status === 401) { router.push(`/api/auth/login?returnUrl=/gallery/${listingId}`); return; }
      const auth = await session.json() as { csrfToken: string };
      const response = await fetch("/api/bff/cart/items", { method: "POST", headers: { "content-type": "application/json", "x-csrf-token": auth.csrfToken }, body: JSON.stringify({ listingId }) });
      const body = await response.json().catch(() => ({})) as { message?: string };
      if (!response.ok) throw new Error(body.message ?? "افزودن به سبد ناموفق بود.");
      router.push("/cart");
    } catch (error) { setMessage(error instanceof Error ? error.message : "خطای ناشناخته"); setBusy(false); }
  }
  return <div className="mt-6"><button type="button" onClick={add} disabled={busy} className="w-full rounded-xl bg-[#251b18] px-6 py-4 font-bold text-white disabled:opacity-50">{busy ? "در حال افزودن…" : "افزودن به سبد خرید"}</button>{message && <p className="mt-3 text-sm text-red-700" role="alert">{message}</p>}</div>;
}
