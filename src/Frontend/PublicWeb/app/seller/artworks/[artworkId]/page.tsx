"use client";

import { use, useEffect, useState } from "react";

type Detail = {
  id: string; internalCode: string;
  revisions: Array<{ id: string; number: number; title: string; status: string; artistAttribution: string; updatedAtUtc: string }>;
  assets: Array<{ id: string; artworkRevisionId: string; kind: string; contentType: string; size: number; sha256: string }>;
};
type ReviewResult = {
  rounds: Array<{ id: string; number: number; status: string }>;
  findings: Array<{ id: string; reviewRoundId: string; category: string; message: string }>;
  decisions: Array<{ id: string; reviewRoundId: string; kind: string; reason: string; publicComment: string }>;
  supersededDecisionIds: string[];
};

export default function ArtworkDetailPage({ params }: { params: Promise<{ artworkId: string }> }) {
  const { artworkId } = use(params); const [detail, setDetail] = useState<Detail>(); const [review, setReview] = useState<ReviewResult>(); const [error, setError] = useState("");
  useEffect(() => { Promise.all([
    fetch(`/api/bff/artworks/${artworkId}`, { cache: "no-store" }),
    fetch(`/api/bff/artworks/${artworkId}/review-result`, { cache: "no-store" })
  ]).then(async ([artworkResponse, reviewResponse]) => {
    if(!artworkResponse.ok) throw new Error("اثر پیدا نشد یا اجازه دسترسی ندارید.");
    setDetail(await artworkResponse.json() as Detail);
    if(reviewResponse.ok) setReview(await reviewResponse.json() as ReviewResult);
  }).catch(e => setError(e instanceof Error ? e.message : "خطا")); }, [artworkId]);
  return <main className="mx-auto min-h-screen max-w-4xl px-6 py-14" dir="rtl">
    <p className="text-sm font-bold tracking-widest text-[#9c4a2f]">جزئیات اثر</p>
    {error && <p className="mt-8 text-red-700">{error}</p>}
    {!detail && !error && <p className="mt-8">در حال دریافت…</p>}
    {detail && <><h1 className="mt-2 text-4xl font-black">{detail.internalCode}</h1>
      <section className="mt-8 grid gap-4">{detail.revisions.map(revision => <article key={revision.id} className="rounded-2xl border border-stone-300 bg-white/75 p-6">
        <div className="flex justify-between gap-4"><strong className="text-xl">نسخه {revision.number}: {revision.title}</strong><span>{revision.status}</span></div>
        <p className="mt-2 text-stone-700">{revision.artistAttribution}</p>
        <ul className="mt-4 space-y-2 text-sm">{detail.assets.filter(a => a.artworkRevisionId === revision.id).map(asset => <li key={asset.id}>{asset.kind} — {asset.contentType} — {asset.size.toLocaleString("fa-IR")} bytes</li>)}</ul>
      </article>)}</section>
      {review && review.rounds.length > 0 && <section className="mt-10 rounded-2xl border border-stone-300 bg-white/75 p-6">
        <h2 className="text-2xl font-black">نتیجه بررسی کارشناسی</h2>
        <div className="mt-4 space-y-5">{review.rounds.map(round => {
          const decision = review.decisions.find(item => item.reviewRoundId === round.id);
          return <article key={round.id} className="border-t border-stone-200 pt-4">
            <strong>دور {round.number}: {round.status}</strong>
            {decision && <div className="mt-2"><p>{decision.publicComment}</p><p className="text-sm text-stone-600">دلیل: {decision.reason}</p>{review.supersededDecisionIds.includes(decision.id) && <span className="text-sm font-bold text-red-700">این تصمیم لغو اعتبار شده است.</span>}</div>}
            <ul className="mt-2 list-disc pr-5">{review.findings.filter(item => item.reviewRoundId === round.id).map(item => <li key={item.id}><b>{item.category}:</b> {item.message}</li>)}</ul>
          </article>;
        })}</div>
      </section>}
      </>}
  </main>;
}
