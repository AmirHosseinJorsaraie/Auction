# ADR-0001: Modular Monolith با چهار لایه Backend

- وضعیت: Proposed
- تاریخ: ۲۰۲۶-۱۰-۱۳

## زمینه

محصول V1 چند حوزه کسب‌وکاری دارد، اما بار، تیم و مرزهای عملیاتی هنوز نیاز به microservice را اثبات نمی‌کنند. در عین حال، یک پروژه واحد بدون مرز، منجر به coupling میان Identity، Catalog، Marketplace و Orders می‌شود.

## تصمیم

- یک deployable Backend مبتنی بر ASP.NET Core 10 ساخته می‌شود.
- پروژه‌های اصلی .NET عبارت‌اند از `Domain`, `Application`, `Infrastructure`, `Contracts`, `Api` و Backoffice مستقل.
- ماژول‌ها در هر لایه با پوشه/namespace و PostgreSQL schema متناظر جدا می‌شوند.
- Domain به هیچ framework persistence/web وابسته نیست؛ Application فقط Domain را می‌شناسد؛ Infrastructure interfaceهای Application را پیاده می‌کند؛ Api composition root است.
- یک DbContext در V1 برای migration و transaction مشترک استفاده می‌شود، ولی configuration و مالکیت table ماژولی است.
- architecture test دسترسی و dependency ممنوع را کنترل می‌کند.

## پیامدها

### مثبت

- deployment و transaction ساده V1
- مرز روشن برای توسعه و تست
- امکان استخراج ماژول در آینده، اگر دلیل عملیاتی ایجاد شود
- حذف network consistency و observability پیچیده microservice

### منفی

- مرزها فقط با discipline و test حفظ می‌شوند و isolation فیزیکی ندارند.
- تغییر اشتباه در Infrastructure می‌تواند table ماژول دیگر را لمس کند؛ code review لازم است.
- یک deployment همه ماژول‌ها را با هم scale می‌کند.

## گزینه‌های ردشده

- **Microservices از روز اول:** پیچیدگی توزیع‌شده بدون نیاز اثبات‌شده.
- **یک پروژه واحد بدون لایه/ماژول:** enforcement و testability ضعیف.
- **یک پروژه برای هر لایه هر ماژول:** project explosion برای V1.

