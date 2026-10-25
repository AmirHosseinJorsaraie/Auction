# ADR-0004: Cart و Order تک‌فروشنده‌ای در V1

- وضعیت: Proposed؛ نیازمند تأیید محصول
- تاریخ: ۲۰۲۶-۱۰-۱۳

## زمینه

سبد چندفروشنده‌ای نیازمند split order، چند shipment، چند policy لغو و احتمالاً settlement جداست. provider، مالیات و مدل تسویه هنوز مشخص نیستند و هر اثر quantity یک دارد.

## تصمیم

- هر Cart و Order فقط یک `SellerUserId` و یک currency دارد.
- چند Artwork همان فروشنده می‌توانند در Cart باشند؛ quantity هر مورد دقیقاً ۱ است.
- افزودن اثر فروشنده دیگر با پیام روشن رد می‌شود یا پس از تأیید کاربر سبد قبلی را جایگزین می‌کند؛ UX نهایی در Phase 6 انتخاب می‌شود.
- Cart reservation ایجاد نمی‌کند. Checkout قیمت و availability را server-side بازخوانی و listingها را اتمیک رزرو می‌کند.
- Order برای هر item price/title/artwork snapshot و برای سفارش shipping address snapshot دارد.
- یک `Order.ReservationExpiresAtUtc` از ساعت دیتابیس deadline تمام itemهاست؛ Reservation فرزند نه deadline و نه eligibility-version مستقل دارد. بنابراین expiry/cancel کل Order چنداثری را all-or-nothing می‌بندد و drift میان itemها در schema قابل ایجاد نیست.

## پیامدها

### مثبت

- یک fulfillment، shipment policy و seller context برای هر سفارش
- transaction و refund آینده ساده‌تر
- UX و عملیات Backoffice قابل فهم‌تر برای V1

### منفی

- خریدار آثار چند فروشنده را در یک پرداخت خریداری نمی‌کند.
- ممکن است conversion کمتر شود و چند checkout لازم باشد.

## شرط بازبینی

درخواست cart چندفروشنده‌ای فقط پس از تعریف payment split/merchant-of-record، shipping و cancellation بازبینی می‌شود.
