# ADR-0005: مرزهای امن Object Storage

- وضعیت: Proposed؛ local implementation در Phase 1 انتخاب می‌شود
- تاریخ: ۲۰۲۶-۱۰-۱۳

## زمینه

محصول هم تصویر قابل نمایش عمومی دارد و هم اصل تصویر، مدارک مالکیت/provenance و verification بسیار حساس. اتکا به نام فایل، object key تصادفی یا prefix داخل bucket عمومی، authorization محسوب نمی‌شود.

## تصمیم

- Backend فقط metadata، SHA-256، classification، clean object key و `VersionId` اختیاری را نگه می‌دارد؛ binary از طریق abstraction سازگار با S3 ذخیره می‌شود.
- `PublicDerivative`, `PrivateOriginal/Document` و `Quarantine` با bucket یا policy مستقل جدا می‌شوند؛ public ACL روی storage ممنوع است.
- upload intent به key/method/size/type/expiry محدود و در Application تک‌مصرف است و فایل ابتدا `PendingScan` می‌شود؛ quarantine key هیچ‌گاه در revision استفاده نمی‌شود.
- scanner magic/size/type/hash/malware را روی همان byte stream یا object version دقیق می‌سنجد. بایت تأییدشده با write server-side و create-if-absent به clean key جدید در namespace/bucket بدون مجوز `Put` کاربر promotion و hash مقصد تطبیق داده می‌شود؛ فقط آن‌گاه status `Clean` است. overwrite دیرهنگام quarantine بی‌اثر است.
- اگر storage source-version pinning قطعی نداشته باشد، scan و promotion از همان stream انجام می‌شوند؛ refetch/copy از key mutable پس از verdict و اعتماد به ETag ممنوع است.
- تصاویر عمومی از clean bytes در سرور decode/re-encode و metadata حساسشان حذف می‌شود و derivative key جدید می‌گیرند. SVG/HTML/archive در V1 پذیرفته نمی‌شوند.
- private download پس از resource authorization با URL بسیار کوتاه‌عمر یا stream کنترل‌شده است؛ signed URL در log ثبت نمی‌شود.
- local S3 implementation بر اساس license، provenance، patch cadence و image قابل pin انتخاب می‌شود. MinIO یا هر گزینه دیگری پیش‌فرض فرض نمی‌شود.

## پیامدها

- انتشار ناخواسته سند خصوصی با misconfiguration یک prefix دشوارتر می‌شود.
- pipeline upload و cleanup پیچیده‌تر است و scanner/storage unavailable باید failure mode مشخص داشته باشد.
- محیط توسعه به image/build قابل اعتماد و pin‌شده نیاز دارد.

## گزینه‌های ردشده

- ذخیره blob در PostgreSQL برای همه فایل‌ها
- bucket عمومی واحد با prefix خصوصی
- اعتماد به MIME/extension مرورگر
- public URL دائمی برای مدارک

