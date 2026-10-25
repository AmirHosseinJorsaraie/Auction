import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { formatMoney, publicApi } from "@/lib/public-api";
export const dynamic = "force-dynamic";
type Data={artist:{name:string;slug:string;biography?:string};listings:Array<{id:string;title:string;priceAmount:number;currency:string}>}; type Props={params:Promise<{slug:string}>};
async function load(slug:string){try{return await publicApi<Data>(`gallery/artists/${encodeURIComponent(slug)}`);}catch{return null;}}
export async function generateMetadata({params}:Props):Promise<Metadata>{const {slug}=await params;const data=await load(slug);return data?{title:data.artist.name,description:data.artist.biography}: {title:"هنرمند پیدا نشد"};}
export default async function Artist({params}:Props){const {slug}=await params;const data=await load(slug);if(!data)notFound();return <main className="mx-auto max-w-5xl px-6 py-14"><p className="text-sm font-bold text-[#9c4a2f]">هنرمند</p><h1 className="mt-2 text-5xl font-black">{data.artist.name}</h1>{data.artist.biography&&<p className="mt-6 max-w-3xl text-lg leading-9 text-stone-700">{data.artist.biography}</p>}<h2 className="mt-12 text-3xl font-black">آثار منتشرشده</h2><div className="mt-6 grid gap-4">{data.listings.map(item=><Link className="flex justify-between rounded-2xl border bg-white/70 p-5" key={item.id} href={`/gallery/${item.id}`}><strong>{item.title}</strong><span>{formatMoney(item.priceAmount,item.currency)}</span></Link>)}</div></main>;}
