export default function Home() {
  return (
    <main className="mx-auto flex min-h-screen max-w-6xl flex-col justify-center px-6 py-20">
      <p className="mb-4 text-sm font-bold tracking-widest text-[#9c4a2f]">ART MARKETPLACE</p>
      <h1 className="max-w-3xl text-5xl font-black leading-tight md:text-7xl">هنر، با روایت و اصالت</h1>
      <p className="mt-7 max-w-2xl text-lg leading-9 text-stone-700">
        زیرساخت نسخه نخست بازار هنر آماده است. در فازهای بعدی، آثار تأییدشده و تجربه خرید امن به این صفحه افزوده می‌شود.
      </p>
      <div className="mt-10 flex flex-wrap gap-3 text-sm">
        <span className="rounded-full border border-stone-400 px-5 py-2">نمایشگاه عمومی</span>
        <span className="rounded-full border border-stone-400 px-5 py-2">بررسی تخصصی</span>
        <span className="rounded-full border border-stone-400 px-5 py-2">خرید امن</span>
      </div>
    </main>
  );
}
