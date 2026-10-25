import type { Metadata } from "next";
import Link from "next/link";
import { ArtworkCard } from "@/components/artwork-card";
import { publicApi, type GalleryPage } from "@/lib/public-api";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "نمایشگاه آثار", description: "جست‌وجو و مشاهده آثار هنری بررسی‌شده", alternates: { canonical: "/gallery" } };

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> };
export default async function Gallery({ searchParams }: Props) {
  const raw = await searchParams; const value = (key: string) => typeof raw[key] === "string" ? raw[key] as string : "";
  const params = new URLSearchParams(); for (const key of ["search", "category", "artist", "sort", "page"]) if (value(key)) params.set(key, value(key));
  let data: GalleryPage | null = null; let failed = false;
  try { data = await publicApi<GalleryPage>(`gallery?${params.toString()}`); } catch { failed = true; }
  return <main className="mx-auto max-w-7xl px-6 py-14"><p className="text-sm font-bold text-[#9c4a2f]">نمایشگاه عمومی</p><h1 className="mt-2 text-5xl font-black">کشف آثار</h1>
    <form className="mt-8 grid gap-3 rounded-3xl border border-stone-300 bg-white/70 p-5 md:grid-cols-[1fr_220px_auto]" action="/gallery">
      <label><span className="sr-only">جست‌وجو</span><input className="w-full rounded-xl border border-stone-300 px-4 py-3" name="search" defaultValue={value("search")} placeholder="نام اثر، هنرمند یا توضیحات" /></label>
      <label><span className="sr-only">مرتب‌سازی</span><select className="w-full rounded-xl border border-stone-300 px-4 py-3" name="sort" defaultValue={value("sort")}><option value="">تازه‌ترین</option><option value="price-asc">قیمت: کم به زیاد</option><option value="price-desc">قیمت: زیاد به کم</option><option value="title">نام اثر</option></select></label>
      <button className="rounded-xl bg-[#251b18] px-6 py-3 font-bold text-white">اعمال</button>
      {value("category") && <input type="hidden" name="category" value={value("category")} />}
      {value("artist") && <input type="hidden" name="artist" value={value("artist")} />}
    </form>
    {failed && <div className="mt-10 rounded-2xl bg-red-50 p-8 text-red-800" role="alert">دریافت آثار موقتاً ممکن نیست. لطفاً دوباره تلاش کنید.</div>}
    {data && data.items.length === 0 && <div className="mt-10 rounded-2xl border border-dashed border-stone-400 p-12 text-center">اثری مطابق جست‌وجوی شما پیدا نشد.</div>}
    {data && data.items.length > 0 && <><p className="mt-8 text-sm text-stone-600">{data.total.toLocaleString("fa-IR")} اثر</p><div className="mt-5 grid gap-7 md:grid-cols-2 lg:grid-cols-3">{data.items.map(item => <ArtworkCard key={item.id} item={item} />)}</div>
      <nav className="mt-12 flex justify-center gap-3" aria-label="صفحه‌بندی">{data.page > 1 && <Link className="rounded-full border px-5 py-2" href={`/gallery?${withPage(params, data.page - 1)}`}>قبلی</Link>}<span className="px-4 py-2">صفحه {data.page.toLocaleString("fa-IR")} از {data.totalPages.toLocaleString("fa-IR")}</span>{data.page < data.totalPages && <Link className="rounded-full border px-5 py-2" href={`/gallery?${withPage(params, data.page + 1)}`}>بعدی</Link>}</nav></>}
  </main>;
}

function withPage(source: URLSearchParams, page: number) { const copy = new URLSearchParams(source); copy.set("page", String(page)); return copy.toString(); }
