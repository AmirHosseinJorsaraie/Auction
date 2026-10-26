# گزارش Phase 6 — سبد خرید، Checkout و سفارش

## نتیجه

سبد خرید سروری، checkout اتمیک، رزرو موقت و تاریخچه سفارش V1 پیاده‌سازی شد. سبد فقط ابزار انتخاب است و هیچ تضمین قیمت یا موجودی ایجاد نمی‌کند؛ تصمیم نهایی فقط در checkout و زیر lockهای دیتابیس گرفته می‌شود.

## چرا این بخش‌ها نوشته شدند

- `Cart` فقط یک فروشنده و یک ارز را می‌پذیرد و quantity در مدل وجود ندارد؛ بنابراین هر اثر یکتا دقیقاً یک واحد است.
- `CartItem.PriceAmountAtAdd` فقط برای تشخیص تغییر قیمت است. قیمت نهایی همیشه از `MarketplaceListing` قفل‌شده خوانده می‌شود.
- `CheckoutPolicy.CanCheckout` فعال‌بودن حساب، تأیید email، تأیید phone و وجود نشانی کامل متعلق به خریدار را متمرکز می‌کند.
- checkout ابتدا userها و seller eligibility، سپس Artworkها و Listingها را با ترتیب قطعی قفل می‌کند تا checkoutهای هم‌زمان فقط یک برنده داشته باشند.
- `Order` مبلغ کل، ارز، خریدار، فروشنده، نشانی ارسال و `SellerEligibilityVersionAtCheckout` را snapshot می‌کند؛ تغییر بعدی profile یا address تاریخچه سفارش را بازنویسی نمی‌کند.
- فقط `Order.ReservationExpiresAtUtc` deadline را نگه می‌دارد. `Reservation` عمداً deadline یا eligibility version ندارد تا دو منبع حقیقت ایجاد نشود.
- idempotency key با unique index خریدار+کلید محافظت می‌شود؛ retry همان درخواست سفارش دوم نمی‌سازد.
- mismatch صلاحیت فروشنده کل checkout را رد می‌کند و listingهای stale را در همان transaction با audit به Suspended می‌برد.
- `ReviewIntegrityHold` و `SellerEligibilityHold` روی سفارش ذخیره می‌شوند. supersession یا revoke هنگام Reserved فروش را جلو نمی‌برد و release بعدی listing را دوباره منتشر نمی‌کند.
- cancel دستی و expiry worker همه reservationها و listingهای یک سفارش را all-or-nothing terminal می‌کنند؛ هر listing فقط پس از اجرای دوباره policy می‌تواند Published شود.

## APIهای افزوده‌شده

- `GET /api/cart`
- `POST /api/cart/items`
- `DELETE /api/cart/items/{listingId}`
- `GET /api/checkout/summary`
- `POST /api/checkout`
- `GET /api/orders/mine`
- `GET /api/orders/{orderId}` با کنترل buyer/seller/staff و جلوگیری از IDOR
- `POST /api/orders/{orderId}/cancel`
- `GET /api/seller/orders`
- `POST /api/seller/orders/{orderId}/processing`
- `POST /api/seller/orders/{orderId}/ship`
- `POST /api/seller/orders/{orderId}/delivered`
- `GET /api/backoffice/orders`

## رابط کاربری

- دکمه افزودن اثر قابل‌خرید به سبد
- صفحه `/cart` با قیمت جاری، هشدار تغییر قیمت، انتخاب نشانی و checkout
- صفحه `/orders` برای تاریخچه سفارش خریدار
- صفحه `/orders/{orderId}` با itemها، نشانی snapshot، hold و timeline وضعیت
- صفحه `/seller/orders` برای نمای پایه فروشنده
- لینک‌های سبد و سفارش در navigation عمومی

## migration و schema

- `20261026145542_Phase6CartCheckoutOrders`
- `20261026145822_Phase6OrderConstraints`
- جدول‌های `orders.carts`, `orders.cart_items`, `orders.orders`, `orders.order_items`, `orders.reservations`
- unique index رزرو فعال روی Listing، idempotency خریدار و item یکتای cart/order
- constraintهای مثبت‌بودن total/price/version
- script استقرار: `artifacts/phase6-idempotent.sql`

## اعتبارسنجی

- build کامل Release با صفر warning/error
- ۵۸ تست غیرکانتینری .NET موفق: ۳۱ Domain، ۱۴ Application، ۸ Architecture، ۴ Integration و ۱ Backoffice
- Public Web: typecheck، lint، unit test و production build موفق
- EF pending-model check: بدون تغییر ثبت‌نشده
- OpenAPI و TypeScript contract دوباره تولید شدند
- migration/race smoke واقعی PostgreSQL به Docker نیاز دارد و در این دستگاه اجرا نشد.

## نکته آموزشی

دکمه checkout فقط یک درخواست optimistic از UI است. مرجع واقعی دیتابیس و transaction است: داده‌های cart دوباره خوانده می‌شوند، قیمت و seller capability تأیید می‌شوند، همه listingها قفل می‌شوند و سپس Order، OrderItem، Reservation و انتقال Listing به Reserved با یک commit ساخته می‌شوند. اگر هر item نامعتبر باشد هیچ سفارش ناقصی باقی نمی‌ماند.

## گام بعدی

Phase 7 لایه پرداخت مستقل از provider، verified receipt، callback idempotent و reconciliation پرداخت دیرهنگام را روی Order رزروشده اضافه می‌کند.
