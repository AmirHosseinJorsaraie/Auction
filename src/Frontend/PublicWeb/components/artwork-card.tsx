import Image from "next/image";
import Link from "next/link";
import { formatMoney, mediaUrl, type GalleryItem } from "@/lib/public-api";

export function ArtworkCard({ item }: { item: GalleryItem }) {
  return <article className="group overflow-hidden rounded-[2rem] border border-stone-300 bg-white shadow-sm transition hover:-translate-y-1 hover:shadow-xl">
    <Link href={`/gallery/${item.id}`} aria-label={`مشاهده ${item.title}`}>
      <div className="relative aspect-[4/3] overflow-hidden bg-stone-200">
        <Image src={mediaUrl(item.imageAssetId)} alt={`تصویر اثر ${item.title}`} fill sizes="(max-width: 768px) 100vw, 33vw" className="object-cover transition duration-500 group-hover:scale-105" />
      </div>
      <div className="p-6"><p className="text-xs font-bold text-[#9c4a2f]">{item.category.name}</p><h2 className="mt-2 text-2xl font-black">{item.title}</h2>
        <p className="mt-1 text-stone-600">{item.artistAttribution}</p><div className="mt-5 flex items-center justify-between gap-3"><strong>{formatMoney(item.priceAmount, item.currency)}</strong><span className={item.available ? "text-emerald-700" : "text-stone-500"}>{item.available ? "آماده خرید" : "فروخته‌شده"}</span></div>
      </div>
    </Link>
  </article>;
}
