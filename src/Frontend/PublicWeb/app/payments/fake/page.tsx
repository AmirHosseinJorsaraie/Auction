"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";

function FakePaymentPanel() {
  const attemptId = useSearchParams().get("attemptId"); const router = useRouter(); const [message, setMessage] = useState(""); const [busy, setBusy] = useState(false);
  async function complete(outcome: "Succeeded" | "Failed" | "Cancelled") {
    if (!attemptId) return; setBusy(true);
    const session = await fetch("/api/auth/session", { cache: "no-store" }); const { csrfToken } = await session.json() as { csrfToken: string };
    const response = await fetch(`/api/bff/payments/fake/${attemptId}/complete`, { method: "POST", headers: { "content-type": "application/json", "x-csrf-token": csrfToken }, body: JSON.stringify({ outcome }) });
    const result = await response.json().catch(() => ({})) as { code?: string; orderId?: string; message?: string };
    if (response.ok) { setMessage(result.code ?? "ثبت شد"); if (result.orderId) setTimeout(() => router.push(`/orders/${result.orderId}`), 800); } else { setMessage(result.message ?? "پردازش نتیجه ناموفق بود."); setBusy(false); }
  }
  return <main className="mx-auto min-h-screen max-w-xl px-6 py-20"><section className="rounded-3xl border bg-white p-8 shadow-sm"><p className="text-sm font-bold text-[#9c4a2f]">درگاه آزمایشی — فقط توسعه</p><h1 className="mt-2 text-3xl font-black">شبیه‌ساز پرداخت</h1><p className="mt-4 leading-8 text-stone-600">این صفحه هیچ سفارشی را مستقیم تغییر نمی‌دهد؛ انتخاب شما یک callback امضاشده می‌سازد و سرور آن را مستقل بررسی می‌کند.</p><div className="mt-8 grid gap-3"><button disabled={busy} onClick={() => complete("Succeeded")} className="rounded-xl bg-emerald-700 px-5 py-3 font-bold text-white">پرداخت موفق</button><button disabled={busy} onClick={() => complete("Failed")} className="rounded-xl border px-5 py-3 font-bold">پرداخت ناموفق</button><button disabled={busy} onClick={() => complete("Cancelled")} className="rounded-xl border px-5 py-3 font-bold">انصراف</button></div>{message && <p role="status" className="mt-6 rounded-xl bg-stone-100 p-4" dir="ltr">{message}</p>}</section></main>;
}

export default function FakePaymentPage() { return <Suspense fallback={<p className="p-8">در حال بارگذاری…</p>}><FakePaymentPanel /></Suspense>; }
