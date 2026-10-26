import Link from "next/link";

export function PublicHeader() {
  return <header className="border-b border-stone-300/80 bg-[#f8f3e9]/90 backdrop-blur">
    <nav className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-4 px-6 py-5" aria-label="ناوبری اصلی">
      <Link href="/" className="text-xl font-black">نگارخانه آبان</Link>
      <div className="flex flex-wrap gap-5 text-sm font-bold">
        <Link href="/gallery">آثار</Link><Link href="/artists">هنرمندان</Link><Link href="/cart">سبد خرید</Link><Link href="/orders">سفارش‌های من</Link><Link href="/about">درباره ما</Link><Link href="/contact">تماس</Link><Link href="/seller/listings">فروش آثار من</Link>
      </div>
    </nav>
  </header>;
}
