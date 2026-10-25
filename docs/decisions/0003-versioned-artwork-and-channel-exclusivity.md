# ADR-0003: Artwork نسخه‌بندی‌شده و exclusivity کانال فروش

- وضعیت: Proposed
- تاریخ: ۲۰۲۶-۱۰-۱۳

## زمینه

کارشناس باید دقیقاً همان محتوایی را تأیید کند که بعداً منتشر می‌شود. در V2 نیز یک اثر نباید هم‌زمان در Marketplace و Auction عرضه شود، ولی ساخت جدول/قابلیت Auction در V1 ممنوع است.

## تصمیم

- `Artwork` هویت پایدار، مالکیت و provenance پایه را نگه می‌دارد.
- اطلاعات قابل بررسی در `ArtworkRevision` نسخه‌بندی می‌شود. هر Draft، `CreatedByOwnerUserId` دارد؛ create/edit/submit زیر `User/SellerEligibility → Artwork → ReviewCase` مالک و capability را دوباره می‌سنجد و submission آن revision را immutable می‌کند.
- `ReviewCase` متعلق به `Artwork` است و هر `ReviewRound` دقیقاً به یک revision ثابت اشاره می‌کند. `MarketplaceListing.PublishedArtworkRevisionId` در Draft تهی است؛ publish پس از revalidation تصمیم مؤثر، همان revision تأییدشده را همراه transition اتمیک pin می‌کند و stateهای فعال بدون pin مجاز نیستند.
- برای هر Artwork حداکثر یک Draft mutable، یک ReviewCase non-terminal و برای هر case یک round `PendingReview` مجاز است؛ partial unique indexها guard نهایی‌اند و submitهای هم‌زمان فقط یک برنده دارند.
- supersession تصمیم، ReviewCase بسته را reopen نمی‌کند؛ همراه safeguard Listing/Order append-only ثبت و به workflow فعال attach می‌شود یا، اگر workflow فعالی نیست، case/round اصلاحی تازه می‌سازد. race آن با submit زیر Artwork/Review lock و retry idempotent حل می‌شود تا unique constraint هیچ‌گاه ابطال approval را خنثی نکند.
- در V1، یک partial unique index بر `MarketplaceListing(ArtworkId)` برای statusهای non-final (`Draft`, `Published`, `Reserved`, `Suspended`, `Sold`) از چند listing باز/درحال‌انتقال جلوگیری می‌کند.
- ساخت Draft پیش از insert، `User/SellerEligibility → Artwork` را lock و owner/capability/نبود listing non-final را revalidate می‌کند؛ index آخرین guard است، نه جایگزین ownership check. در V2، channel claim متعارض نیز Draft جدید را رد می‌کند.
- تمام publish/reinstate/release/revision-swap/deactivateها از policy متمرکز عبور می‌کنند و owner، seller/account eligibility، approval مؤثر، hold، invalidation و channel constraint را live بررسی می‌کنند. checkout نیز snapshot eligibility هر Listing را با نسخه جاری و invalidation history می‌سنجد و حق refresh ضمنی ندارد؛ فقط resolution ممیزی‌شده + reinstate/re-publish snapshot را تازه می‌کند.
- در V2 یک جدول مشترک مانند `ArtworkChannelClaim` با `ArtworkId` به‌عنوان PK اضافه می‌شود. Marketplace و Auction فقط در transaction با claim می‌توانند active شوند. `Draft` claim نمی‌گیرد؛ `Published/Reserved/Suspended` و `Sold` تا انتقال مالکیت claim را نگه می‌دارند.
- تغییر کانال یک workflow صریح `deactivate → release claim → eligibility/review → acquire claim → activate` است؛ هیچ تبدیل خودکار listing به lot وجود ندارد. پیش از فعال‌سازی Auction، Draftهای Marketplace همان Artwork با reason ممیزی‌شده اتمیک Withdraw می‌شوند؛ انتقال مالکیت نیز Draft revision باقی‌مانده مالک قبلی را terminal و ReviewCase/round باز او را withdraw می‌کند، بی‌آنکه revision/approval تاریخی را بازنویسی یا حق edit را به مالک جدید واگذار کند.
- create/edit/submit revision، create Listing Draft، supersession و مسیرهای eligibility invalidation/publish/reinstate/revision-swap/checkout/expiry/cancel-release/payment/ownership-transfer برای یک اثر از guard/lock order مشترک عبور می‌کنند؛ واکنش به state جاری Artwork/Review/Listing/Order زیر lock دوباره محاسبه می‌شود.

## پیامدها

- تغییرات پس از review نمی‌توانند approval قبلی را به محتوای جدید تعمیم دهند.
- تاریخچه و قابلیت اثبات تصمیم حفظ می‌شود.
- V1 abstraction یا enum مربوط به Auction نمی‌سازد.
- migration V2 نیازمند backfill listingهای claim-holding، withdrawal Draftهای stale در activation/ownership transfer و پنجره maintenance/dual-write کنترل‌شده است، اما V1 هیچ artifact حراج نمی‌سازد.

## گزینه‌های ردشده

- یک enum بزرگ روی Artwork برای review/listing/order: lifecycleهای مستقل را مخلوط می‌کند.
- aggregate عمومی `Sale/Offering` برای Marketplace و Auction در V1: abstraction حدسی و coupling زودهنگام.
- ویرایش مستقیم Artwork تأییدشده: audit و اعتماد review را می‌شکند.

