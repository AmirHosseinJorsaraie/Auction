# گزارش Phase 4 — بررسی کارشناسی و Moderation

## نتیجه

گردش‌کار بررسی کارشناسی V1 پیاده‌سازی شد. هر تصمیم به `ArtworkRevisionId` مشخص pin می‌شود، تاریخچه تصمیم/یافته/annotation/supersession فقط append می‌شود و دو تصمیم نهایی رقیب با optimistic concurrency و unique constraint مهار می‌شوند.

## چرا این ساختار نوشته شد

- `ReviewAssignment` مالک عملیاتی هر دور را مشخص می‌کند تا هر دارنده نقش اداری نتواند تصادفی تصمیم بگیرد.
- policy به claim صریح `Reviews.Decide` وابسته است؛ نقش `Admin` به‌تنهایی اختیار review ندارد. این permission به‌طور پیش‌فرض برای `Expert` و `SuperAdmin` seed می‌شود.
- `ReviewFinding` شواهد را با visibility خصوصی یا seller-visible نگه می‌دارد.
- `ReviewDecision` immutable است و revision دیده‌شده، actor، reason، public comment و private note را ثبت می‌کند.
- `ReviewDecisionSupersession` تصمیم قبلی را پاک یا بازنویسی نمی‌کند؛ یک رکورد جدا و یک review safeguard جدید می‌سازد.
- `ReviewAnnotation` توضیح غیرماهوی را بدون دست‌کاری تصمیم تاریخی اضافه می‌کند.
- `ReviewRound.Version` برای optimistic concurrency است و unique index روی decision هر round فقط یک برنده را می‌پذیرد.
- notification به Outbox همان transaction نوشته می‌شود؛ خرابی ارسال بعدی تصمیم ثبت‌شده را rollback نمی‌کند.

## APIهای افزوده‌شده

- `GET /api/backoffice/reviews/queue`
- `GET /api/backoffice/reviews/{caseId}`
- `POST /api/backoffice/reviews/{caseId}/rounds/{roundId}/assign`
- `POST /api/backoffice/reviews/{caseId}/rounds/{roundId}/findings`
- `POST /api/backoffice/reviews/{caseId}/rounds/{roundId}/decision`
- `POST /api/backoffice/reviews/decisions/{decisionId}/annotations`
- `POST /api/backoffice/reviews/decisions/{decisionId}/supersede`
- `GET /api/artworks/{artworkId}/review-result`
- `POST /api/artworks/{artworkId}/revisions` برای revision جدید پس از request changes

## رابط کاربری

- صفحه فارسی `/reviews` در Backoffice برای دریافت assignment، ثبت finding و تصمیم نهایی.
- proxy سمت سرور Backoffice access token را به API می‌فرستد و token در JavaScript مرورگر قرار نمی‌گیرد.
- صفحه جزئیات اثر فروشنده فقط comment و finding مجاز را نمایش می‌دهد و private note را اصلاً از API دریافت نمی‌کند.

## migration و بررسی

- `20261025124706_Phase4ExpertReviewModeration`
- `20261025124946_Phase4ReviewAnnotations`
- script استقرار idempotent: `artifacts/phase4-idempotent.sql`
- build Release با صفر warning/error پاس شد.
- ۳۳ تست غیرکانتینری پاس شد (۲۰ Domain، ۱ Application، ۸ Architecture، ۳ Integration و ۱ Backoffice).
- migration smoke واقعی PostgreSQL همچنان به Docker نیاز دارد و در این دستگاه اجرا نشد.

## بررسی دستی پیشنهادی

۱. با حساب Expert+MFA وارد Backoffice شوید و یک round را assign کنید.
۲. یک finding خصوصی و یک finding قابل مشاهده برای فروشنده ثبت کنید.
۳. هم‌زمان از دو session روی یک `ExpectedVersion` تصمیم بفرستید؛ فقط یکی باید موفق شود.
۴. `RequestChanges` بدهید، با seller revision جدید بسازید و دوباره submit کنید.
۵. از seller view تأیید کنید private note نمایش داده نمی‌شود.
۶. با SuperAdmin یک decision را supersede کنید و ایجاد safeguard case/round جدید را بررسی کنید.

## محدودیت عملیاتی باقی‌مانده

ارسال واقعی ایمیل/پیام هنوز provider ندارد؛ eventها durable در Outbox ثبت می‌شوند. اجرای migration و race test واقعی روی PostgreSQL/CI پس از فراهم‌شدن Docker لازم است.
