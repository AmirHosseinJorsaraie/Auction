import type { Metadata } from "next";
import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import { formatMoney, mediaUrl, publicApi, type ListingDetail } from "@/lib/public-api";

export const dynamic = "force-dynamic";
type Props = { params: Promise<{ listingId: string }> };
async function load(id: string) { try { return await publicApi<ListingDetail>(`gallery/${id}`); } catch { return null; } }
export async function generateMetadata({ params }: Props): Promise<Metadata> { const { listingId } = await params; const item = await load(listingId); return item ? { title: item.title, description: item.description.slice(0, 155), alternates: { canonical: `/gallery/${listingId}` }, openGraph: { title: item.title, description: item.description.slice(0, 155) } } : { title: "اثر پیدا نشد" }; }
export default async function Artwork({ params }: Props) {
  const { listingId } = await params; const item = await load(listingId); if (!item) notFound();
  return <main className="mx-auto max-w-7xl px-6 py-14"><div className="grid gap-12 lg:grid-cols-[1.1fr_.9fr]">
    <section><div className="relative aspect-[4/3] overflow-hidden rounded-[2.5rem] bg-stone-200"><Image src={mediaUrl(item.images[0]?.id)} alt={`تصویر اثر ${item.title}`} fill priority sizes="(max-width: 1024px) 100vw, 55vw" className="object-cover" /></div>
      {item.images.length > 1 && <div className="mt-4 grid grid-cols-4 gap-3">{item.images.slice(1).map(image => <div key={image.id} className="relative aspect-square overflow-hidden rounded-xl"><Image src={mediaUrl(image.id)} alt={`نمای دیگر از ${item.title}`} fill sizes="20vw" className="object-cover" /></div>)}</div>}</section>
    <section><Link href={`/gallery?category=${item.category.slug}`} className="text-sm font-bold text-[#9c4a2f]">{item.category.name}</Link><h1 className="mt-3 text-5xl font-black">{item.title}</h1><p className="mt-3 text-xl text-stone-600">{item.artist ? <Link href={`/artists/${item.artist.slug}`}>{item.artist.name}</Link> : item.artistAttribution}</p>
      <p className="mt-8 text-3xl font-black">{formatMoney(item.priceAmount, item.currency)}</p><p className={`mt-2 font-bold ${item.available ? "text-emerald-700" : "text-stone-500"}`}>{item.available ? "این اثر آماده خرید است" : "این اثر دیگر قابل خرید نیست"}</p>
      <div className="mt-8 leading-8 text-stone-700">{item.description}</div><dl className="mt-8 grid grid-cols-2 gap-4 rounded-2xl bg-white/70 p-6"><Info label="سال" value={item.year?.toLocaleString("fa-IR") ?? "نامشخص"}/><Info label="تکنیک" value={item.medium}/><Info label="ابعاد" value={item.dimensions}/><Info label="وضعیت" value={item.condition}/></dl>
      <div className="mt-8 border-t border-stone-300 pt-6"><p className="text-sm text-stone-500">ارائه‌شده توسط</p><h2 className="mt-1 text-xl font-black">{item.seller.displayName}</h2>{item.seller.bio && <p className="mt-2 leading-7 text-stone-600">{item.seller.bio}</p>}</div>
    </section></div></main>;
}
function Info({ label, value }: { label: string; value: string }) { return <div><dt className="text-xs text-stone-500">{label}</dt><dd className="mt-1 font-bold">{value}</dd></div>; }
