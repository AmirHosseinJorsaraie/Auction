# ADR-0007 — ذخیره‌ساز شیء محلی

- وضعیت: پذیرفته‌شده برای توسعه محلی
- تاریخ: ۲۰۲۶-۱۰-۱۷

## تصمیم

برای محیط توسعه و CI محلی از SeaweedFS با S3 gateway استفاده می‌شود. image دقیق `chrislusf/seaweedfs:4.41` با digest در `deploy/docker-compose.yml` pin شده است. کد کاربردی فقط به قرارداد S3 وابسته خواهد بود و این انتخاب، provider تولید را تعیین نمی‌کند.

## دلیل

- پروژه فعال، متن‌باز و دارای مجوز Apache-2.0 است.
- S3 API را بدون وابستگی دامنه به vendor ارائه می‌دهد.
- یک node محلی برای smoke test کافی است، ولی topology تولیدی باید جداگانه طراحی شود.

## ملاحظات امنیتی و عملیاتی

- credentialهای موجود در repository فقط placeholder محلی‌اند و برای هیچ محیط مشترک/تولید معتبر نیستند.
- bucketهای خصوصی، quarantine و promotion امن در Phase 3 پیاده‌سازی می‌شوند.
- digest باید توسط Dependabot/Renovate یا بازبینی دوره‌ای به‌روزرسانی شود؛ tag به‌تنهایی مبنای اعتماد نیست.
- patch، provenance و آسیب‌پذیری image در hardening نهایی دوباره بررسی می‌شود.
