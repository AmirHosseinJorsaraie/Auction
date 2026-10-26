"use client";

import { useParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";

type Detail = { order: { id: string; totalAmount: number; currency: string; status: string; reservationExpiresAtUtc: string; recipientName: string; addressLine: string; postalCode: string; holdReason?: string; version: number }; items: Array<{ id: string; title: string; priceAmount: number; currency: string }> };
type Attempt = { id: string; status: string; redirectUrl?: string; failureCode?: string };
const steps = ["AwaitingPayment", "Confirmed", "Processing", "Shipped", "Delivered"];

export default function OrderDetailPage() {
  const { orderId } = useParams<{ orderId: string }>();
  const [data, setData] = useState<Detail>();
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const paymentKey = useRef<string | null>(null);
  if (paymentKey.current === null) paymentKey.current = crypto.randomUUID();
  useEffect(() => { fetch(`/api/bff/orders/${orderId}`, { cache: "no-store" }).then(async response => { if (!response.ok) throw new Error(); setData(await response.json() as Detail); }).catch(() => setMessage("سفارش پیدا نشد یا اجازه مشاهده آن را ندارید.")); }, [orderId]);
  async function csrf() { const response = await fetch("/api/auth/session", { cache: "no-store" }); return (await response.json() as { csrfToken: string }).csrfToken; }
  async function pay() {
    setBusy(true); setMessage("");
    const response = await fetch(`/api/bff/orders/${orderId}/payment-attempts`, { method: "POST", headers: { "content-type": "application/json", "x-csrf-token": await csrf() }, body: JSON.stringify({ idempotencyKey: paymentKey.current }) });
    const body = await response.json().catch(() => ({})) as Attempt & { message?: string; detail?: string };
    if (response.ok && body.redirectUrl) window.location.assign(body.redirectUrl);
    else { setMessage(body.message ?? body.detail ?? "شروع پرداخت ممکن نشد؛ دوباره تلاش کنید."); setBusy(false); }
  }
  async function cancel() {
    if (!data) return;
    const response = await fetch(`/api/bff/orders/${orderId}/cancel`, { method: "POST", headers: { "content-type": "application/json", "x-csrf-token": await csrf() }, body: JSON.stringify({ expectedVersion: data.order.version }) });
    if (response.ok) location.reload(); else { const body = await response.json().catch(() => ({})) as { message?: string }; setMessage(body.message ?? "لغو سفارش ناموفق بود."); }
  }
  return <main className="mx-auto min-h-screen max-w-4xl px-6 py-14">{message && <p role="alert" className="rounded-xl bg-red-50 p-4 text-red-800">{message}</p>}{data && <><p className="text-sm font-bold text-[#9c4a2f]">جزئیات سفارش</p><h1 className="mt-2 text-3xl font-black" dir="ltr">#{data.order.id}</h1><ol className="mt-8 grid gap-2 md:grid-cols-5">{steps.map((step, index) => <li key={step} className={`rounded-xl p-3 text-center text-sm ${steps.indexOf(data.order.status) >= index ? "bg-[#251b18] text-white" : "bg-stone-200"}`}>{step}</li>)}</ol><section className="mt-8 grid gap-3">{data.items.map(item => <div key={item.id} className="flex justify-between rounded-2xl border bg-white/80 p-5"><strong>{item.title}</strong><span>{item.priceAmount.toLocaleString("fa-IR")} {item.currency}</span></div>)}</section><section className="mt-8 rounded-2xl bg-white/80 p-6"><h2 className="font-black">نشانی ارسال</h2><p className="mt-3 leading-8">{data.order.recipientName}<br />{data.order.addressLine}<br />{data.order.postalCode}</p></section>{data.order.holdReason && <p className="mt-6 rounded-xl bg-amber-50 p-4 text-amber-900">{data.order.holdReason}</p>}{data.order.status === "AwaitingPayment" && <div className="mt-6 flex flex-wrap gap-3"><button disabled={busy} onClick={pay} className="rounded-xl bg-[#251b18] px-6 py-3 font-bold text-white disabled:opacity-50">{busy ? "در حال اتصال…" : "پرداخت امن"}</button><button onClick={cancel} className="rounded-xl border border-red-400 px-5 py-3 font-bold text-red-800">لغو سفارش</button></div>}</>}</main>;
}
