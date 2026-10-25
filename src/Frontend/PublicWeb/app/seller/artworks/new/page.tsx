"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";

const schema = z.object({
  title: z.string().trim().min(1).max(200), artistAttribution: z.string().trim().min(1).max(200),
  description: z.string().trim().min(1).max(5000), categoryId: z.string().uuid(),
  year: z.coerce.number().int().min(0).max(3000).optional(), medium: z.string().trim().min(1).max(200),
  dimensions: z.string().trim().min(1).max(200), condition: z.string().trim().min(1).max(1000),
});
type Values = z.input<typeof schema>;
type ValidValues = z.output<typeof schema>;
type Category = { id: string; name: string };
type Session = { authenticated: boolean; csrfToken: string };

async function session(): Promise<Session> {
  const response = await fetch("/api/auth/session", { cache: "no-store" });
  if (!response.ok) throw new Error("ابتدا وارد حساب شوید.");
  return response.json() as Promise<Session>;
}

export default function NewArtworkPage() {
  const [categories, setCategories] = useState<Category[]>([]);
  const [artworkId, setArtworkId] = useState<string>();
  const [message, setMessage] = useState("");
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<Values, unknown, ValidValues>({ resolver: zodResolver(schema) });
  useEffect(() => { fetch("/api/bff/categories").then(r => r.ok ? r.json() : []).then(setCategories); }, []);

  const create = handleSubmit(async values => {
    setMessage("");
    try {
      const auth = await session();
      const response = await fetch("/api/bff/artworks", { method: "POST", headers: { "content-type": "application/json", "x-csrf-token": auth.csrfToken }, body: JSON.stringify(values) });
      const body = await response.json(); if (!response.ok) throw new Error(body.title ?? body.message ?? "ثبت اثر ناموفق بود.");
      setArtworkId(body.artworkId as string); setMessage("پیش‌نویس ساخته شد. اکنون تصویر یا مدرک را بارگذاری کنید.");
    } catch (error) { setMessage(error instanceof Error ? error.message : "خطای ناشناخته"); }
  });

  async function upload(file: File, kind: "public-image" | "private-original" | "private-document") {
    if (!artworkId) return;
    setMessage("در حال بارگذاری و بررسی فایل…");
    try {
      const auth = await session();
      const intentResponse = await fetch(`/api/bff/artworks/${artworkId}/uploads/intents`, { method: "POST", headers: { "content-type": "application/json", "x-csrf-token": auth.csrfToken }, body: JSON.stringify({ fileName: file.name, contentType: file.type, size: file.size, kind }) });
      const intent = await intentResponse.json(); if (!intentResponse.ok) throw new Error(intent.title ?? "ایجاد مجوز بارگذاری ناموفق بود.");
      const uploadResponse = await fetch(intent.uploadUrl as string, { method: "PUT", headers: { "content-type": file.type, "x-amz-meta-expected-length": String(file.size) }, body: file });
      if (!uploadResponse.ok) throw new Error("ارسال فایل به quarantine ناموفق بود.");
      const finalize = await fetch(`/api/bff/artworks/${artworkId}/uploads/${intent.id}/finalize`, { method: "POST", headers: { "x-csrf-token": auth.csrfToken } });
      const result = await finalize.json(); if (!finalize.ok) throw new Error(result.title ?? "بررسی امنیتی فایل ناموفق بود.");
      setMessage(`فایل پاک‌سازی و ثبت شد. SHA-256: ${result.sha256}`);
    } catch (error) { setMessage(error instanceof Error ? error.message : "خطای ناشناخته"); }
  }

  async function submitForReview() {
    if (!artworkId) return; const auth = await session();
    const response = await fetch(`/api/bff/artworks/${artworkId}/submit`, { method: "POST", headers: { "x-csrf-token": auth.csrfToken } });
    setMessage(response.ok ? "نسخه فریز شد و برای بررسی ارسال گردید." : "ارسال برای بررسی ناموفق بود؛ وجود تصویر عمومی پاک را بررسی کنید.");
  }

  return <main className="mx-auto min-h-screen max-w-3xl px-6 py-14" dir="rtl">
    <p className="text-sm font-bold tracking-widest text-[#9c4a2f]">ARTWORK INTAKE</p><h1 className="mt-2 text-4xl font-black">ثبت اثر جدید</h1>
    <p className="mt-4 leading-8 text-stone-700">ابتدا metadata را ثبت کنید. اعتبارسنجی مرورگر برای راهنمایی است؛ قوانین اصلی دوباره در سرور اجرا می‌شوند.</p>
    <form onSubmit={create} className="mt-8 grid gap-5 rounded-3xl border border-stone-300 bg-white/75 p-7">
      <Field label="عنوان اثر" error={errors.title?.message}><input {...register("title")} /></Field>
      <Field label="نام هنرمند" error={errors.artistAttribution?.message}><input {...register("artistAttribution")} /></Field>
      <Field label="دسته‌بندی" error={errors.categoryId?.message}><select {...register("categoryId")}><option value="">انتخاب کنید</option>{categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></Field>
      <Field label="توضیحات" error={errors.description?.message}><textarea rows={5} {...register("description")} /></Field>
      <div className="grid gap-5 md:grid-cols-2"><Field label="سال خلق"><input type="number" {...register("year")} /></Field><Field label="تکنیک / متریال"><input {...register("medium")} /></Field></div>
      <div className="grid gap-5 md:grid-cols-2"><Field label="ابعاد"><input {...register("dimensions")} /></Field><Field label="وضعیت اثر"><input {...register("condition")} /></Field></div>
      <button disabled={isSubmitting || !!artworkId} className="rounded-xl bg-stone-900 px-5 py-3 text-white disabled:opacity-50">{isSubmitting ? "در حال ثبت…" : "ساخت پیش‌نویس"}</button>
    </form>
    {artworkId && <section className="mt-7 rounded-3xl border border-stone-300 bg-white/75 p-7">
      <h2 className="text-2xl font-bold">فایل‌های اثر</h2><p className="mt-2 text-sm text-stone-600">JPEG/PNG برای تصویر و PDF برای مدرک؛ فایل ابتدا وارد quarantine می‌شود.</p>
      <div className="mt-5 grid gap-4 md:grid-cols-3">
        <Upload label="تصویر عمومی" accept="image/jpeg,image/png" onFile={f => upload(f,"public-image")} />
        <Upload label="اصل خصوصی" accept="image/jpeg,image/png" onFile={f => upload(f,"private-original")} />
        <Upload label="مدرک خصوصی" accept="application/pdf" onFile={f => upload(f,"private-document")} />
      </div>
      <button onClick={submitForReview} className="mt-6 rounded-xl bg-[#9c4a2f] px-5 py-3 text-white">فریز و ارسال برای بررسی</button>
    </section>}
    {message && <p className="mt-6 rounded-xl border border-stone-300 bg-white p-4 leading-7">{message}</p>}
  </main>;
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return <label className="grid gap-2 font-bold">{label}<span className="[&_input]:w-full [&_input]:rounded-lg [&_input]:border [&_input]:p-3 [&_select]:w-full [&_select]:rounded-lg [&_select]:border [&_select]:p-3 [&_textarea]:w-full [&_textarea]:rounded-lg [&_textarea]:border [&_textarea]:p-3">{children}</span>{error && <small className="text-red-700">{error}</small>}</label>;
}
function Upload({ label, accept, onFile }: { label: string; accept: string; onFile: (file: File) => void }) {
  return <label className="cursor-pointer rounded-xl border border-dashed border-stone-500 p-4 text-center font-bold">{label}<input className="mt-3 block w-full text-xs" type="file" accept={accept} onChange={e => { const file=e.target.files?.[0]; if(file) onFile(file); }} /></label>;
}
