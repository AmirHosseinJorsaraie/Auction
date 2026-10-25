"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

type Artwork = { id: string; internalCode: string; createdAtUtc: string };

export default function MyArtworksPage() {
  const [items, setItems] = useState<Artwork[]>([]);
  const [state, setState] = useState<"loading" | "ready" | "unauthenticated" | "error">("loading");

  useEffect(() => {
    fetch("/api/bff/artworks/mine", { cache: "no-store" }).then(async (response) => {
      if (response.status === 401) return setState("unauthenticated");
      if (!response.ok) return setState("error");
      setItems((await response.json()) as Artwork[]); setState("ready");
    }).catch(() => setState("error"));
  }, []);

  return <main className="mx-auto min-h-screen max-w-5xl px-6 py-16" dir="rtl">
    <div className="flex items-center justify-between gap-4">
      <div><p className="text-sm font-bold tracking-widest text-[#9c4a2f]">پنل فروشنده</p><h1 className="mt-2 text-4xl font-black">آثار من</h1></div>
      <Link className="rounded-xl bg-stone-900 px-5 py-3 text-white" href="/seller/artworks/new">ثبت اثر جدید</Link>
    </div>
    {state === "loading" && <p className="mt-10">در حال دریافت آثار…</p>}
    {state === "unauthenticated" && <p className="mt-10">برای مشاهده آثار <a className="underline" href="/api/auth/login?returnUrl=/seller/artworks">وارد شوید</a>.</p>}
    {state === "error" && <p className="mt-10 text-red-700">دریافت اطلاعات ناموفق بود.</p>}
    {state === "ready" && <section className="mt-10 grid gap-4 md:grid-cols-2">
      {items.length === 0 && <p>هنوز اثری ثبت نکرده‌اید.</p>}
      {items.map(item => <Link key={item.id} href={`/seller/artworks/${item.id}`} className="rounded-2xl border border-stone-300 bg-white/70 p-6">
        <strong className="block text-xl">{item.internalCode}</strong><span className="mt-2 block text-sm text-stone-600">{new Date(item.createdAtUtc).toLocaleString("fa-IR")}</span>
      </Link>)}
    </section>}
  </main>;
}
