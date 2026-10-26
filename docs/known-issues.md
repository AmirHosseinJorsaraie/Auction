# ریسک‌ها و تصمیم‌های باز

> «باز» بودن یک موضوع به معنی توقف کل پروژه نیست. ستون owner نقش تصمیم‌گیر/تأییدکننده و ستون deadline آخرین زمان امن تصمیم را مشخص می‌کند.

| شناسه | شدت | موضوع | پیشنهاد فعلی / اثر | owner / approver | deadline |
|---|---|---|---|---|---|
| KI-001 | بالا | کشور، ارز، merchant-of-record، مالیات و مقررات نامشخص | Money/currency configurable؛ provider واقعی ممنوع | Product + Legal/Finance | پیش از Phase 7 و production |
| KI-002 | حل‌شده در Phase 2 | topology نهایی `www/auth/admin/api/media` | hostهای مستقل و قابل تنظیم با OIDC+BFF و cookieهای host-only؛ هیچ cookie مشترک دامنه‌ای | Platform + Security | Phase 2 |
| KI-003 | حل‌شده در Phase 2 | self-hosted OpenIddict یا IdP مدیریت‌شده | baseline V1 خودمیزبان Identity + OpenIddict پذیرفته و قرارداد OIDC استاندارد برای امکان جایگزینی حفظ شد | Architecture + Security + Operations | Phase 2 |
| KI-004 | بالا | معیار، مدارک، expiry و retention verification فروشنده | workflow عمومی بدون حدس نوع مدرک؛ invalidation canonical فوراً فروش را fail-closed و listing/order را state-specific hold/suspend می‌کند | Product + Legal/Security | پیش از Phase 2/3 |
| KI-005 | بالا | cancellation/return/refund/dispute تعریف نشده | transition پس از پرداخت محدود بماند | Product + Legal/Finance | پیش از Phase 6/7 |
| KI-006 | حل‌شده فنی در Phase 7 / تصمیم refund باز | مدت reservation و late payment policy | deadline configurable و late success در reconciliation ثبت می‌شود؛ اقدام refund نیازمند تصمیم حقوقی/مالی است | Product + Payments | پیش از production |
| KI-007 | بالا | امنیت upload و provider اسکن بدافزار | quarantine، allow-list و scan fail-closed؛ provider باز | Security + Platform | پیش از Phase 3 |
| KI-008 | بالا / guarded در Phase 7 | زمان انتقال مالکیت حقوقی اثر | مسیر اتمیک پیاده شد ولی feature switch پیش‌فرض خاموش است؛ Listing تا trigger مصوب `Sold` می‌ماند | Product + Legal | پیش از فعال‌سازی/Phase 9 |
| KI-009 | متوسط | فروشنده لزوماً مالک است یا consignment مجاز؟ | V1: `SellerId == CurrentOwnerId` | Product | پیش از Phase 3 |
| KI-010 | متوسط | تعریف تغییر substantive | پیشنهاد: همه فیلدهای عمومی/تصاویر/provenance review مجدد | Product + Review Operations | پیش از Phase 3/4 |
| KI-011 | متوسط | assignment، conflict و تعداد کارشناسان | منع self-review قطعی؛ queue/assignment باز | Product + Review Operations | پیش از Phase 4 |
| KI-012 | متوسط | policy Artist | Artist مستقل و attribution snapshot؛ جزئیات curated باز | Product | پیش از Phase 3/5 |
| KI-013 | متوسط | حمل، هزینه، محدوده، tracking و مسئول خسارت | shipping snapshot پایه؛ ruleها باز | Product + Operations | پیش از Phase 6 |
| KI-014 | متوسط | cart تک‌فروشنده‌ای نیازمند تأیید محصول | ADR-0004 پیشنهاد فعال | Product | پیش از Phase 6 |
| KI-015 | حل‌شده در Phase 1 | local S3 implementation | SeaweedFS 4.41 با digest ثابت انتخاب و در ADR-0007 ثبت شد؛ hardening bucket/promotion در Phase 3 | Platform + Security | Phase 1 |
| KI-016 | متوسط | کانال notification و SLA | notification خارج transaction؛ provider باز | Product + Operations | پیش از Phase 4 |
| KI-017 | متوسط | audit/log retention و tamper-resistance | داده کمینه/redacted؛ مدت نگهداری و نیاز WORM باز | Security + Legal | پیش از Phase 8/9 |
| KI-018 | متوسط | SLO، ترافیک، RPO/RTO و hosting | baseline در Phase 9 بدون اعداد حدسی | Platform + Product | پیش از Phase 9 |
| KI-019 | متوسط | تقویم شمسی، currency/number display | storage UTC؛ display قابل تنظیم | Product + UX/Finance | پیش از Phase 5 |
| KI-020 | حل‌شده در Phase 1 | نام محصول و root repository | ریشه به `D:\Projects\Auction` و solution به `ArtMarketplace.sln` تثبیت شد | User + Architecture | Phase 1 |
| KI-021 | بالا | Docker محلی موجود نیست | .NET 10.0.401 و Node 24.19.0 به‌صورت portable اعتبارسنجی شدند؛ Compose/Testcontainers باید روی Docker محلی یا CI اجرا شوند | Platform | پیش از بستن Phase 1 |
| KI-022 | متوسط | اطلاعات تماس لازم برای `CanCheckout` مشخص نیست | email/phone/address و زمان تأیید باید با نیاز حقوقی و حمل تعیین شود | Product + Legal | پیش از Phase 2/6 |
| KI-023 | بالا | moderation یا ابطال approval برای Listing در `Reserved/Sold` policy نهایی ندارد | rollback تجاری ممنوع؛ Reserved می‌تواند Order/Reservation را terminal ولی Listing را فقط Suspended/locked کند؛ Sold روی hold/incident می‌ماند و auto-sale/re-publish/unlock ممنوع است تا refund/legal workflow تصویب شود | Product + Legal + Payments/Operations | پیش از Phase 6/7 |
| KI-024 | متوسط | حساب عملیاتی staff جدا از حساب خرید/فروش همان فرد | برای least privilege پیشنهاد می‌شود؛ نیازمند تصمیم محصول/عملیات | Security + Product | پیش از Phase 2 |
| KI-025 | متوسط | settlement currency با display unit یکی فرض نشود | ISO currency/provider amount از نمایش ریال/تومان و conversion config جدا باشد | Finance + Architecture | پیش از Phase 3/7 |
| KI-026 | متوسط | advisory در dependency ابزار lint | پنج finding high فقط در dev dependency زنجیره `eslint-config-next/fast-glob/micromatch/braces`؛ production audit پاک و fix سازگار فعلاً منتشر نشده است | Frontend + Security | بازبینی دوره‌ای/پیش از Phase 9 |
| KI-027 | بالا | حفاظت key ring در محیط مشترک | volume پایدار تنظیم شده، اما keyهای محلی encrypt-at-rest نیستند؛ KMS/certificate، access policy و rotation پیش از استقرار مشترک لازم است | Platform + Security | پیش از staging/Phase 9 |

## ریسک‌های امنیتی که باید با تست پوشش داده شوند

- IDOR برای Artwork، Order، Review و PrivateDocument
- CSRF روی mutationهای Public/Backoffice
- token/cookie leakage و session fixation
- privilege escalation و stale role/verification claim
- self-review یا دسترسی بیش از حد Expert
- upload polyglot، MIME spoof، malware و public ACL اشتباه
- race در checkout، reservation expiry و payment callback
- replay/duplicate/out-of-order webhook
- fake payment فعال در Production
- log شدن token، OTP، signed URL، document یا PII

## بدهی‌های ممنوع در V1

- enum یکپارچه برای Artwork/Review/Listing/Order
- ذخیره price روی Artwork
- API/UI/table/job مربوط به Auction یا verification سطح ۳
- generic `Sale` یا `Offering` فقط برای نیاز فرضی V2
- JWT/refresh token در browser storage
- public URL دائمی برای document خصوصی
- authorization صرفاً در UI
- generic repository بی‌دلیل روی EF Core
- message broker/Redis بدون requirement اندازه‌گیری‌شده
