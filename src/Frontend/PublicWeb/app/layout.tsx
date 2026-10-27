import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./globals.css";
import { PublicHeader } from "@/components/public-header";
import { PublicBanner } from "@/components/public-banner";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.SITE_URL ?? process.env.PUBLIC_SITE_URL ?? "https://marketplace.localhost"),
  title: { default: "نگارخانه آبان", template: "%s | نگارخانه آبان" },
  description: "بازار آنلاین آثار هنری بررسی‌شده و اصیل",
  openGraph: { title: "نگارخانه آبان", description: "بازار آنلاین آثار هنری بررسی‌شده و اصیل", type: "website", locale: "fa_IR" },
};

export default function RootLayout({ children }: Readonly<{ children: ReactNode }>) {
  return (
    <html lang="fa" dir="rtl">
      <body><a className="skip-link" href="#main-content">رفتن به محتوای اصلی</a><PublicBanner /><PublicHeader /><div id="main-content" tabIndex={-1}>{children}</div><footer className="mt-20 border-t border-stone-300 px-6 py-10 text-center text-sm text-stone-600">© نگارخانه آبان — هنر با روایت و اصالت</footer></body>
    </html>
  );
}
