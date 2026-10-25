import Link from "next/link";
import { ArtworkCard } from "@/components/artwork-card";
import { publicApi, type GalleryPage } from "@/lib/public-api";

export const dynamic = "force-dynamic";

export default async function Home() {
  let featured: GalleryPage = { items: [], page: 1, pageSize: 6, total: 0, totalPages: 0 };
  try { featured = await publicApi<GalleryPage>("gallery?pageSize=6"); } catch { /* graceful empty state */ }
  return (
    <main>
      <section className="mx-auto grid min-h-[70vh] max-w-7xl items-center gap-12 px-6 py-20 lg:grid-cols-[1.2fr_.8fr]">
        <div><p className="mb-4 text-sm font-bold tracking-[.3em] text-[#9c4a2f]">CURATED ART MARKETPLACE</p><h1 className="max-w-4xl text-5xl font-black leading-tight md:text-7xl">هنر، با روایت و اصالت</h1>
          <p className="mt-7 max-w-2xl text-lg leading-9 text-stone-700">مجموعه‌ای گزیده از آثار بررسی‌شده؛ با اطلاعات روشن، نسخه تأییدشده و تجربه‌ای آرام برای کشف هنر.</p>
          <div className="mt-10 flex gap-3"><Link className="rounded-full bg-[#251b18] px-7 py-3 font-bold text-white" href="/gallery">مشاهده آثار</Link><Link className="rounded-full border border-stone-500 px-7 py-3 font-bold" href="/about">داستان ما</Link></div>
        </div><div className="hero-mark" aria-hidden="true"><span>آ</span></div>
      </section>
      <section className="mx-auto max-w-7xl px-6 py-16"><div className="mb-8 flex items-end justify-between"><div><p className="text-sm font-bold text-[#9c4a2f]">تازه‌های نگارخانه</p><h2 className="mt-2 text-4xl font-black">آثار منتخب</h2></div><Link href="/gallery" className="font-bold">مشاهده همه ←</Link></div>
        {featured.items.length ? <div className="grid gap-7 md:grid-cols-2 lg:grid-cols-3">{featured.items.map(item => <ArtworkCard key={item.id} item={item} />)}</div> : <div className="rounded-3xl border border-dashed border-stone-400 p-12 text-center text-stone-600">آثار تأییدشده به‌زودی در این بخش نمایش داده می‌شوند.</div>}
      </section>
    </main>
  );
}
