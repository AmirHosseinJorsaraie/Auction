import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { publicApi } from "@/lib/public-api";

type ContentPage = { slug: string; title: string; body: string; updatedAtUtc: string };
async function content(slug: string) { try { return await publicApi<ContentPage>(`public/content/pages/${encodeURIComponent(slug)}`); } catch { return null; } }
export async function generateMetadata({ params }: { params: Promise<{ slug: string }> }): Promise<Metadata> { const { slug } = await params; const page = await content(slug); return { title: page?.title ?? "صفحه پیدا نشد", robots: page ? undefined : { index: false } }; }
export default async function InformationPage({ params }: { params: Promise<{ slug: string }> }) { const { slug } = await params; const page = await content(slug); if (!page) notFound(); return <main className="mx-auto min-h-[60vh] max-w-3xl px-6 py-16"><p className="text-sm font-bold text-[#9c4a2f]">اطلاع‌رسانی</p><h1 className="mt-2 text-4xl font-black">{page.title}</h1><div className="mt-8 whitespace-pre-wrap rounded-3xl border border-stone-300 bg-white/80 p-8 leading-9">{page.body}</div><p className="mt-4 text-sm text-stone-500">آخرین به‌روزرسانی: {new Date(page.updatedAtUtc).toLocaleDateString("fa-IR")}</p></main>; }
