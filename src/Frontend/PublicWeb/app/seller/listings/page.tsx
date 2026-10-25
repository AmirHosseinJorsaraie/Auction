"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";

type Listing = {
  id: string;
  artworkId: string;
  internalCode: string;
  priceAmount: number;
  currency: string;
  status: string;
  publishedArtworkRevisionId?: string;
  suspensionReason?: string;
  version: number;
  approvedRevisionIds: string[];
};

type Session = { authenticated: boolean; csrfToken: string };
type PageState = "loading" | "ready" | "unauthenticated" | "error";

const statusLabels: Record<string, string> = {
  Draft: "پیش‌نویس",
  Published: "منتشرشده",
  Suspended: "تعلیق‌شده",
  Withdrawn: "خارج‌شده",
  Reserved: "رزروشده",
  Sold: "فروخته‌شده",
  Completed: "تکمیل‌شده",
};

export default function SellerListingsPage() {
  const [items, setItems] = useState<Listing[]>([]);
  const [state, setState] = useState<PageState>("loading");
  const [message, setMessage] = useState("");
  const [busyId, setBusyId] = useState<string>();

  const load = useCallback(async () => {
    try {
      const response = await fetch("/api/bff/marketplace/listings/mine", { cache: "no-store" });
      if (response.status === 401) return setState("unauthenticated");
      if (!response.ok) return setState("error");
      setItems((await response.json()) as Listing[]);
      setState("ready");
    } catch {
      setState("error");
    }
  }, []);

  useEffect(() => {
    fetch("/api/bff/marketplace/listings/mine", { cache: "no-store" }).then(async response => {
      if (response.status === 401) return setState("unauthenticated");
      if (!response.ok) return setState("error");
      setItems((await response.json()) as Listing[]);
      setState("ready");
    }).catch(() => setState("error"));
  }, []);

  async function change(listing: Listing, operation: "publish" | "reinstate" | "revision" | "withdraw") {
    setBusyId(listing.id); setMessage("");
    try {
      const sessionResponse = await fetch("/api/auth/session", { cache: "no-store" });
      if (!sessionResponse.ok) throw new Error("برای ادامه باید وارد حساب شوید.");
      const session = await sessionResponse.json() as Session;
      const revisionId = listing.approvedRevisionIds[0];
      if (operation !== "withdraw" && !revisionId) throw new Error("نسخه تأییدشده‌ای برای انتشار وجود ندارد.");
      const body = operation === "withdraw"
        ? { expectedVersion: listing.version }
        : { revisionId, expectedVersion: listing.version };
      const response = await fetch(`/api/bff/marketplace/listings/${listing.id}/${operation}`, {
        method: "POST",
        headers: { "content-type": "application/json", "x-csrf-token": session.csrfToken },
        body: JSON.stringify(body),
      });
      const result = await response.json().catch(() => ({})) as { message?: string; title?: string };
      if (!response.ok) throw new Error(result.message ?? result.title ?? "عملیات فهرست فروش ناموفق بود.");
      setMessage("تغییر با موفقیت و پس از بررسی دوباره شرایط فروشنده و اثر ثبت شد.");
      await load();
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "خطای ناشناخته");
    } finally {
      setBusyId(undefined);
    }
  }

  return <main className="mx-auto min-h-screen max-w-6xl px-6 py-14" dir="rtl">
    <div className="flex flex-wrap items-end justify-between gap-4">
      <div><p className="text-sm font-bold text-[#9c4a2f]">پنل فروشنده</p><h1 className="mt-2 text-4xl font-black">فهرست‌های فروش من</h1><p className="mt-3 max-w-2xl leading-8 text-stone-600">انتشار فقط برای نسخه تأییدشده انجام می‌شود و سرور در همان تراکنش، مالکیت، احراز فروشنده و اعتبار حساب را دوباره بررسی می‌کند.</p></div>
      <Link className="rounded-xl border border-stone-400 px-5 py-3 font-bold" href="/seller/artworks">مدیریت آثار</Link>
    </div>
    {state === "loading" && <p className="mt-10">در حال دریافت فهرست‌ها…</p>}
    {state === "unauthenticated" && <p className="mt-10">برای مدیریت فروش <a className="underline" href="/api/auth/login?returnUrl=/seller/listings">وارد شوید</a>.</p>}
    {state === "error" && <p className="mt-10 text-red-700" role="alert">دریافت فهرست‌ها ناموفق بود.</p>}
    {message && <p className="mt-8 rounded-2xl border border-stone-300 bg-white p-4" role="status">{message}</p>}
    {state === "ready" && <section className="mt-10 grid gap-5">
      {items.length === 0 && <div className="rounded-2xl border border-dashed border-stone-400 p-10 text-center">هنوز فهرست فروشی ندارید. فهرست اولیه هنگام ثبت اثر ساخته می‌شود.</div>}
      {items.map(listing => <article key={listing.id} className="rounded-3xl border border-stone-300 bg-white/80 p-6 shadow-sm">
        <div className="flex flex-wrap items-start justify-between gap-4"><div><h2 className="text-xl font-black">{listing.internalCode}</h2><p className="mt-2 text-sm text-stone-600">{money(listing.priceAmount, listing.currency)}</p></div><span className="rounded-full bg-stone-100 px-4 py-2 text-sm font-bold">{statusLabels[listing.status] ?? listing.status}</span></div>
        {listing.suspensionReason && <p className="mt-4 rounded-xl bg-amber-50 p-4 text-amber-900">دلیل تعلیق: {listing.suspensionReason}</p>}
        <p className="mt-4 text-sm text-stone-600">نسخه‌های قابل انتشار: {listing.approvedRevisionIds.length.toLocaleString("fa-IR")}</p>
        <div className="mt-5 flex flex-wrap gap-3">
          {listing.status === "Draft" && <Action disabled={busyId === listing.id || !listing.approvedRevisionIds.length} onClick={() => change(listing, "publish")}>انتشار</Action>}
          {listing.status === "Suspended" && <Action disabled={busyId === listing.id || !listing.approvedRevisionIds.length} onClick={() => change(listing, "reinstate")}>فعال‌سازی دوباره</Action>}
          {listing.status === "Published" && listing.approvedRevisionIds.some(id => id !== listing.publishedArtworkRevisionId) && <Action disabled={busyId === listing.id} onClick={() => change(listing, "revision")}>انتشار آخرین نسخه تأییدشده</Action>}
          {["Draft", "Published", "Suspended"].includes(listing.status) && <Action secondary disabled={busyId === listing.id} onClick={() => change(listing, "withdraw")}>خروج از فروش</Action>}
          {listing.status === "Published" && <Link className="rounded-xl border border-stone-400 px-4 py-2 font-bold" href={`/gallery/${listing.id}`}>مشاهده عمومی</Link>}
        </div>
      </article>)}
    </section>}
  </main>;
}

function Action({ children, disabled, secondary, onClick }: { children: React.ReactNode; disabled: boolean; secondary?: boolean; onClick: () => void }) {
  return <button type="button" disabled={disabled} onClick={onClick} className={`rounded-xl px-4 py-2 font-bold disabled:cursor-not-allowed disabled:opacity-40 ${secondary ? "border border-stone-400" : "bg-[#251b18] text-white"}`}>{children}</button>;
}

function money(amount: number, currency: string) {
  try { return new Intl.NumberFormat("fa-IR", { style: "currency", currency, maximumFractionDigits: 0 }).format(amount); }
  catch { return `${amount.toLocaleString("fa-IR")} ${currency}`; }
}
