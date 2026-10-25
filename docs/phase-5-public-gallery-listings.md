# گزارش Phase 5 — گالری عمومی و فهرست‌های فروش

## نتیجه

گالری عمومی RTL و گردش‌کار انتشار فهرست فروش V1 پیاده‌سازی شد. محتوای عمومی فقط از نسخه دقیق تأییدشده خوانده می‌شود و هر عملیات انتشار در یک تراکنش Serializable، مالکیت اثر، وضعیت حساب، صلاحیت فروشنده، اعتبار تأیید کارشناسی، invalidation حل‌نشده و hold فعال را دوباره بررسی می‌کند.

## چرا این ساختار نوشته شد

- `ListingPublicationPolicy.CanPublishListingNow` قانون مرکزی انتشار است تا endpointهای publish، reinstate و revision swap تفسیر متفاوتی از شرایط نداشته باشند.
- `PublishedArtworkRevisionId` نسخه نمایش‌داده‌شده را دقیقاً pin می‌کند؛ بنابراین ویرایش بعدی اثر بدون تأیید وارد گالری نمی‌شود.
- `SellerEligibilityVersionAtPublish` snapshot صلاحیت فروشنده است. تغییر نسخه صلاحیت، listing قدیمی را فوراً از projection عمومی حذف می‌کند و فقط revalidation ممیزی‌شده می‌تواند آن را برگرداند.
- `Version` روی listing optimistic concurrency ایجاد می‌کند تا دو فرمان هم‌زمان بی‌صدا روی هم نوشته نشوند.
- unique index شرطی اجازه نمی‌دهد برای یک اثر بیش از یک listing غیرنهایی وجود داشته باشد.
- supersession تصمیم کارشناسی و revoke صلاحیت فروشنده، listing منتشرشده را در همان تراکنش معلق می‌کنند. query عمومی نیز مستقل از worker همان شرایط را دوباره کنترل می‌کند و fail-closed است.
- API عمومی projection محدود برمی‌گرداند: عنوان، مشخصات اثر، قیمت، وضعیت دسترس‌پذیری و اطلاعات عمومی فروشنده؛ ایمیل، سند خصوصی و object key در قرارداد عمومی وجود ندارند.

## APIهای افزوده‌شده

- `GET /api/gallery` با search، category، artist، sort و pagination قطعی
- `GET /api/gallery/{listingId}`
- `GET /api/gallery/artists`
- `GET /api/gallery/artists/{slug}`
- `GET /api/marketplace/listings/mine`
- `POST /api/marketplace/listings/{listingId}/publish`
- `POST /api/marketplace/listings/{listingId}/reinstate`
- `POST /api/marketplace/listings/{listingId}/revision`
- `POST /api/marketplace/listings/{listingId}/withdraw`
- `POST /api/backoffice/listings/{listingId}/suspend`

## رابط کاربری

- صفحه خانه و بخش آثار منتخب
- گالری با جست‌وجو، مرتب‌سازی، صفحه‌بندی، loading، empty و error state
- صفحه جزئیات اثر، دسته‌بندی، فهرست هنرمندان و پروفایل هنرمند
- صفحات درباره ما و تماس
- صفحه فارسی `/seller/listings` برای انتشار، فعال‌سازی دوباره، تعویض نسخه و خروج از فروش
- metadata، canonical، OpenGraph، sitemap و proxy محدود تصویر عمومی
- صفحات عمومی read-heavy به‌صورت Server Component و مرز تعاملی فروشنده به‌صورت Client Component نوشته شده‌اند.

## migration و اعتبارسنجی

- migration: `20261025143817_Phase5PublicGalleryListings`
- script استقرار idempotent: `artifacts/phase5-idempotent.sql`
- `dotnet build ArtMarketplace.sln -c Release --no-restore`: موفق با صفر warning و error
- ۴۴ تست غیرکانتینری .NET: موفق
- Public Web: typecheck، lint، unit test و production build همگی موفق
- EF pending-model check: بدون تغییر ثبت‌نشده
- migration smoke واقعی PostgreSQL به Docker نیاز دارد و در این دستگاه اجرا نشد.

## نکات آموزشی مهم

انتشار یک تغییر ظاهری status نیست. policy فقط تصمیم می‌گیرد، aggregate transition معتبر را اعمال می‌کند، transaction شرایط خوانده‌شده را پایدار نگه می‌دارد و constraint دیتابیس آخرین خط دفاع در race است. در بخش عمومی نیز UI مرجع امنیت نیست؛ query سرور دوباره صلاحیت و approval مؤثر را کنترل می‌کند.

Server Component برای صفحه‌ای مناسب است که عمدتاً داده می‌خواند، چون HTML اولیه قابل استفاده و مناسب SEO تولید می‌کند. Client Component فقط در صفحه مدیریت فروشنده استفاده شده، چون به state مرورگر، session و فرمان‌های تعاملی نیاز دارد.

## گام بعدی

Phase 6 سبد خرید تک‌فروشنده‌ای، checkout، رزرو اتمیک و Order snapshotها را اضافه می‌کند. آن فاز باید در لحظه checkout دوباره همین listing pin و eligibility snapshot را اعتبارسنجی کند و هرگز listing قدیمی را ضمنی refresh نکند.
