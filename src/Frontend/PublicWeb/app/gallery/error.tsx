"use client";
export default function ErrorPage({ reset }: { reset: () => void }) { return <main className="mx-auto max-w-3xl px-6 py-24 text-center"><h1 className="text-4xl font-black">نمایش آثار ممکن نشد</h1><p className="mt-4 text-stone-600">خطای موقتی رخ داده است.</p><button onClick={reset} className="mt-8 rounded-full bg-[#251b18] px-6 py-3 text-white">تلاش دوباره</button></main>; }
