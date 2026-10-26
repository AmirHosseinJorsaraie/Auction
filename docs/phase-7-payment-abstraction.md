# گزارش Phase 7 — Payment Abstraction

## هدف و نتیجه

این فاز یک مرز پرداخت مستقل از provider اضافه می‌کند. Fake Provider فقط ابزار Development/Test است و انتخاب آن در Production در startup شکست قطعی ایجاد می‌کند. موفقیت مرورگر هیچ وضعیت مالی را تغییر نمی‌دهد؛ فقط callback امضاشده، تازه و تأییدشده سمت سرور پردازش می‌شود.

## چرا مدل‌ها جدا هستند؟

- `PaymentAttempt`: تلاش durable و idempotent برای اتصال یک Order به provider، همراه snapshot مبلغ، ارز، merchant account و reference.
- `PaymentProviderEvent`: هر event معتبر provider به‌صورت append-only؛ event تکراری با `(ProviderKey, EventId)` مهار می‌شود.
- `VerifiedPaymentReceipt`: واقعیت دریافت وجه برای یک transaction واقعی؛ با `(ProviderKey, ProviderTransactionId)` یکتا است.
- `OrderPaymentApplication`: مشخص می‌کند دقیقاً کدام receipt به Order اعمال شد؛ `OrderId` و `VerifiedPaymentReceiptId` هر دو unique هستند.
- `PaymentReconciliationCase`: late success، receipt اضافه، mismatch یا hold را بدون تحریف واقعیت مالی نگه می‌دارد.

این جداسازی مهم است چون «پول دریافت شد» الزاماً به معنی «اجازه فروش/ارسال وجود دارد» نیست.

## جریان initiation

1. خریدار با idempotency key درخواست پرداخت می‌دهد.
2. API مالکیت Order، deadline و hold را بررسی و `PaymentAttempt` را commit می‌کند.
3. transaction دیتابیس بسته می‌شود.
4. provider فراخوانی می‌شود.
5. reference/redirect یا failure روی attempt ثبت می‌شود.

پس network call داخل transaction دیتابیس نیست و crash پس از مرحله ۲ یک recovery record قابل مشاهده باقی می‌گذارد.

## جریان callback و fail-closed

Fake adapter امضای HMAC را با مقایسه constant-time بررسی می‌کند، timestamp باید در freshness window باشد و raw payload فقط با SHA-256 digest نگهداری می‌شود. secret، signature و payload خام log نمی‌شوند. پیش از `Confirmed/Sold` شدن، callback موارد زیر را دوباره می‌سنجد:

- provider transaction، merchant reference و merchant account
- amount و currency نسبت به attempt و Order
- deadline یکتای Order و `AwaitingPayment`
- integrity/eligibility hold
- نسخه جاری SellerEligibility و invalidation حل‌نشده
- مجموعه دقیق Listing/Artwork/Revision سفارش و approval مؤثر
- نبود application قبلی برای Order

هر mismatch یک receipt واقعی و یک reconciliation می‌سازد، ولی application و فروش صفر می‌ماند.

## Fake Provider و UI

صفحه `/payments/fake` فقط در توسعه callback امضاشده success/failure/cancel می‌سازد. حتی این صفحه نیز Order را مستقیم تغییر نمی‌دهد. endpoint عمومی `/api/payments/return` فقط پیام informational می‌دهد.

## انتقال مالکیت

endpoint staff برای `Sold → Completed` وجود دارد، اما `Payments:OwnershipTransferEnabled` پیش‌فرض `false` است تا trigger حقوقی بدون تصمیم محصول حدس زده نشود. در حالت فعال نیز Order باید Delivered، payment application موجود و همه holdها خاموش باشند. transaction انتقال:

- exact OrderItem/Listing/Artwork/Application و from/to را pin می‌کند؛
- Draftهای mutable مالک قبلی را با `OwnershipChanged` discarded می‌کند؛
- review case/round باز همان مالک را withdraw می‌کند؛
- owner را تغییر و Listing را Completed می‌کند؛
- `OwnershipTransfer` append-only و idempotent می‌سازد.

## APIهای اصلی

- `POST /api/orders/{orderId}/payment-attempts`
- `GET /api/payments/attempts/{attemptId}`
- `POST /api/payments/callbacks/{providerKey}`
- `GET /api/payments/return`
- `POST /api/payments/fake/{attemptId}/complete` فقط Development
- `GET /api/backoffice/payments/reconciliation`
- `POST /api/backoffice/orders/{orderId}/ownership-transfer`

## بررسی دستی

1. Docker stack را در Development اجرا و Order بسازید.
2. در جزئیات Order روی «پرداخت امن» بزنید و success را در Fake page انتخاب کنید.
3. Order باید Confirmed و Listing باید Sold شود.
4. همان event را replay کنید؛ event/receipt/application دوم نباید ساخته شود.
5. با override مبلغ/ارز نامعتبر callback بفرستید؛ receipt و reconciliation ساخته شود ولی Order AwaitingPayment بماند.
6. API را با `ASPNETCORE_ENVIRONMENT=Production` و `Payments__Provider=Fake` اجرا کنید؛ startup باید fail-fast شود.

## محدودیت‌های صریح

- provider واقعی، refund/dispute و merchant-of-record هنوز تصمیم حقوقی/مالی ندارند و عمداً پیاده‌سازی نشده‌اند.
- انتقال حقوقی پیش‌فرض خاموش است.
- Docker روی این ماشین در دسترس نبود؛ migration و raceهای واقعی PostgreSQL باید در Docker/CI اجرا شوند.

## نتیجه اعتبارسنجی

- `dotnet build ArtMarketplace.sln -c Release --no-restore`: موفق، صفر warning و صفر error.
- ۶۷ تست غیرکانتینری .NET موفق: Domain=37، Application=14، Architecture=8، Integration بدون Testcontainers=7 و Backoffice=1.
- `dotnet ef migrations has-pending-model-changes`: بدون model drift.
- Production startup با Fake Provider: مطابق انتظار fail-fast با `The Fake payment provider cannot run in Production`.
- Public Web با Node 24.19.0: typecheck، lint، یک تست Vitest و production build همگی موفق.
- تست Testcontainers/PostgreSQL اجرا شد اما به‌دلیل نبود Docker محلی شکست زیرساختی داشت؛ failure پنهان یا به‌عنوان pass گزارش نشده است.
