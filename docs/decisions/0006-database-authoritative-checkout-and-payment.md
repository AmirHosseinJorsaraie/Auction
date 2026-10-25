# ADR-0006: PostgreSQL مرجع رزرو، فروش و نتیجه پرداخت

- وضعیت: Proposed
- تاریخ: ۲۰۲۶-۱۰-۱۳

## زمینه

اثر فیزیکی quantity یک دارد. دو checkout هم‌زمان، expiry هم‌زمان با callback، retry provider و late success نباید double-sell یا side effect تکراری بسازند.

## تصمیم

- Cart موجودی را رزرو نمی‌کند؛ checkout وضعیت و قیمت را server-side دوباره می‌خواند.
- برای checkout چنداثری، `ListingId`ها با ترتیب قطعی lock/update می‌شوند. همه transitionهای `Published → Reserved` و ایجاد Order/snapshot/reservation در یک transaction و all-or-nothing هستند.
- ترتیب مشترک `UserAccount/SellerEligibility → Artwork → ReviewCase → Listing → Order → Payment` است؛ مسیرهای create/edit/submit revision، create Listing Draft، invalidation/supersession/publish-reinstate/revision-swap/checkout/expiry/cancel-release/callback/ownership-transfer، ownership/eligibility/state/approval/hold را زیر lock دوباره validate می‌کنند.
- PostgreSQL row lock/conditional update و concurrency version مرجع‌اند؛ Redis/distributed lock اضافه نمی‌شود.
- checkout با ساعت دیتابیس یک `Order.ReservationExpiresAtUtc` یکتا می‌سازد؛ Reservation فرزند deadline مستقل ندارد. expiry هم در مسیرهای حساس lazy بررسی و هم با `BackgroundService` batch/idempotent پاک‌سازی می‌شود. واحد worker یک Order کامل است: SellerEligibility guard و سپس همه Artwork guardهای مرتب با `SKIP LOCKED` گرفته می‌شوند و مجموعه باید کامل باشد؛ در غیر این صورت no-op/rollback. سپس Review/Listing/Order lock، deadline یکتای Order و همه itemها/holdها revalidate و all-or-nothing terminal می‌شوند؛ held/ineligible Listing به `Suspended` می‌رود، نه `Published`.
- قبل از فراخوانی provider، Order/Reservation/PaymentAttempt durable commit می‌شوند؛ network call داخل transaction دیتابیس نیست.
- idempotency key، provider transaction id و event id constraint یکتا دارند، اما همه receiptهای واقعاً متفاوت حتی اگر چند مورد موفق باشند ثبت می‌شوند. هر `VerifiedPaymentReceipt` به event موفق دقیق متصل است و `OrderPaymentApplication` با FK به همان receipt و unique constraint روی `OrderId` تعیین می‌کند دقیقاً کدام دریافت حداکثر یک‌بار به Order اعمال شده است.
- پس از verification خارج از transaction، handler شناسه‌ها را برای برنامه lock می‌خواند و در transaction کوتاه `User/SellerEligibility → Artwork → ReviewCase → Listing → Order` را با ترتیب قطعی lock/revalidate می‌کند؛ سپس event/receipt را idempotent ثبت و application یا reconciliation را می‌سازد. successهای متفاوت هم‌زمان با guard مشترک سریال می‌شوند. conflict یکتایی غیرمنتظره باید با همان کلیدها retry شود تا receipt دوم rollback دائمی نشود؛ acknowledgement فقط پس از commit است.
- SellerEligibility guard مفقود/نامعتبر، expiry را به مقصد `Suspended` و payment را به مسیر receipt + reconciliation می‌برد؛ authorization نامعتبر receipt مالی واقعی را حذف نمی‌کند.
- publish/reinstate، `EligibilityVersion` را روی Listing و checkout نسخه جاری را فقط روی Order pin می‌کند؛ Reservation آن را از Order والد می‌گیرد. هر invalidation، `InvalidatedVersion` و work item durable را در همان transaction ثبت می‌کند و تمام snapshotهای کوچک‌تر را می‌پوشاند؛ reapproval آن را پاک/resolve نمی‌کند و resolution target-specific append-only است.
- checkout زیر lock، current/valid بودن eligibility، برابری snapshot همه Listingها با نسخه جاری و نبود invalidation حل‌نشده را کنترل می‌کند. یک item stale کل checkout را بدون Order/Reservation رد و staleها را با audit `Suspended` می‌کند؛ checkout حق refresh ضمنی snapshot Listing ندارد.
- callback/release/transfer هم snapshot Order و هم تمام Listingها، برابری آن‌ها با نسخه جاری و invalidation history را می‌سنجند. mismatch/expiry در payment، receipt معتبر را حفظ و application را صفر و reconciliation را یک می‌کند؛ در release مقصد `Suspended` است.
- webhook امضا/secret، freshness، replay، merchant account، amount/currency/reference را کنترل و در صورت امکان server-to-server verify می‌کند.
- success واجد شرایط فقط همراه ایجاد اتمیک `OrderPaymentApplication`، Order را Confirmed و Listing را Sold می‌کند؛ Listing تا انتقال صریح مالکیت و `Completed` شدن lock را نگه می‌دارد.
- cancel/expiry زیر guard، Order/همه Reservationها/Listingها را all-or-nothing terminal/release می‌کند؛ فقط owner + seller/account eligibility + approval مؤثر + نبود hold اجازه `Reserved → Published` می‌دهد، وگرنه مقصد `Suspended` است. late receipt پس از cancel فقط reconciliation است.
- انتقال `Sold → Completed` یک `OwnershipTransfer` یکتای متصل به همان OrderItem/Listing/Artwork/OrderPaymentApplication می‌سازد؛ current owner/from باید seller سفارش و to دقیقاً buyer باشد. transfer + terminal کردن Draft mutable و review باز مالک قبلی + owner update + Completed اتمیک‌اند و هر review/eligibility/fulfillment hold آن را fail-closed می‌کند؛ تاریخچه frozen/published حفظ و edit right به خریدار واگذار نمی‌شود؛ trigger حقوقی باید پیش‌تر تصویب شود.
- late success پس از expiry یا receipt موفق اضافه، نتیجه provider را بدون تحریف ثبت و فقط `PaymentReconciliationCase` ایجاد می‌کند؛ اثر خودکار فروخته نمی‌شود.
- Fake Provider فقط در Development/Test register می‌شود و Production با انتخاب آن fail-fast است.

## پیامدها

- correctness به transaction دیتابیس متکی است و با scale افقی نیز قابل حفظ می‌ماند.
- transaction باید کوتاه باشد و provider call خارج از آن مدیریت retry/recovery داشته باشد.
- تست integration واقعی PostgreSQL برای race، deadlock retry، duplicate/out-of-order callback و crash cleanup اجباری است.

## گزینه‌های ردشده

- lock سمت browser یا اعتماد به وضعیت cache
- Redis lock به‌عنوان منبع نهایی موجودی
- Paid کردن Order با redirect مرورگر
- فراخوانی provider داخل transaction باز دیتابیس
