import Link from "next/link";
import type { Metadata } from "next";
import { publicApi } from "@/lib/public-api";
export const dynamic = "force-dynamic"; export const metadata: Metadata = { title: "هنرمندان", alternates: { canonical: "/artists" } };
type Artist = { id:string; name:string; slug:string; artworkCount:number };
export default async function Artists() { let artists: Artist[]=[]; try { artists=await publicApi<Artist[]>("gallery/artists"); } catch {} return <main className="mx-auto max-w-6xl px-6 py-14"><h1 className="text-5xl font-black">هنرمندان</h1>{artists.length ? <div className="mt-10 grid gap-4 md:grid-cols-2">{artists.map(a => <Link className="rounded-2xl border border-stone-300 bg-white/70 p-6" key={a.id} href={`/artists/${a.slug}`}><strong className="text-2xl">{a.name}</strong><p className="mt-2 text-stone-600">{a.artworkCount.toLocaleString("fa-IR")} اثر</p></Link>)}</div> : <p className="mt-10">هنرمندی با اثر منتشرشده وجود ندارد.</p>}</main>; }
