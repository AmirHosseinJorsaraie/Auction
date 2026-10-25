import type { Metadata } from "next";
import { ArtworkCard } from "@/components/artwork-card";
import { publicApi, type GalleryPage } from "@/lib/public-api";
export const dynamic = "force-dynamic";
type Props = { params: Promise<{ slug: string }> };
export async function generateMetadata({ params }: Props): Promise<Metadata> { const { slug } = await params; return { title: `دسته ${slug}`, alternates: { canonical: `/categories/${slug}` } }; }
export default async function Category({ params }: Props) { const { slug } = await params; let data: GalleryPage = {items:[],page:1,pageSize:48,total:0,totalPages:0}; try { data = await publicApi<GalleryPage>(`gallery?category=${encodeURIComponent(slug)}&pageSize=48`); } catch {} return <main className="mx-auto max-w-7xl px-6 py-14"><p className="text-sm font-bold text-[#9c4a2f]">دسته‌بندی</p><h1 className="mt-2 text-5xl font-black">{data.items[0]?.category.name ?? slug}</h1>{data.items.length ? <div className="mt-10 grid gap-7 md:grid-cols-2 lg:grid-cols-3">{data.items.map(item => <ArtworkCard key={item.id} item={item}/>)}</div> : <p className="mt-10">اثری در این دسته منتشر نشده است.</p>}</main>; }
