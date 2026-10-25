# راهنمای یادگیری بک‌اند پروژه بازار هنر

این سند توضیح می‌دهد در پروژه چه چیزی ساخته شده، هر بخش کد چه مسئولیتی دارد، چرا این طراحی انتخاب شده، یک درخواست چگونه در سیستم حرکت می‌کند و هنگام خطا باید از کجا بررسی را شروع کنید.

> نکته مهم: کد فعلی شامل زیرساخت Phase 1 و قابلیت‌های Identity، Profile و Seller Verification در Phase 2 است. بخش‌هایی مثل Catalog، آپلود اثر، Expert Review، Listing، Cart، Order و Payment فعلاً طراحی شده‌اند اما هنوز کد عملیاتی آن‌ها نوشته نشده است. مستند معماری را با قابلیت پیاده‌سازی‌شده اشتباه نگیرید.

## ۱. تصویر ذهنی اصلی پروژه

معماری پروژه یک **Modular Monolith** با اصول **Clean Architecture** است. یعنی فعلاً به‌جای چند Microservice، یک کدبیس منسجم داریم؛ اما مرز ماژول‌ها و لایه‌ها مشخص است تا رشد پروژه باعث درهم‌ریختگی نشود.

قانون اصلی این است:

> وابستگی کد باید به سمت قوانین کسب‌وکار حرکت کند؛ قوانین کسب‌وکار نباید به دیتابیس، HTTP یا Framework وابسته باشند.

![جهت وابستگی در Clean Architecture](../assets/art-marketplace-backend-guide-illustrations/01-clean-architecture.png)

### مسئولیت پروژه‌ها

| پروژه | چه کاری انجام می‌دهد؟ | چرا جدا شده است؟ |
|---|---|---|
| `ArtMarketplace.Domain` | مدل‌ها، قوانین، invariantها و تغییر وضعیت‌های مجاز | قوانین اصلی بدون EF Core، ASP.NET و PostgreSQL قابل تست می‌مانند. |
| `ArtMarketplace.Application` | قرارداد use caseها، interfaceها و policyهای کاربردی | لایه داخلی می‌گوید چه چیزی نیاز دارد؛ Infrastructure نحوه انجام آن را تعیین می‌کند. |
| `ArtMarketplace.Infrastructure` | EF Core، PostgreSQL، Identity storage، session، health check و clock | جزئیات تکنولوژی از منطق کسب‌وکار جدا می‌شوند. |
| `ArtMarketplace.Contracts` | DTOهای مربوط به ارتباط HTTP | مدل انتقال داده، entity دیتابیس یا Domain object را به client نشت نمی‌دهد. |
| `ArtMarketplace.Api` | HTTP pipeline، احراز هویت، مجوزها، rate limit و endpointها | نقطه ورود درخواست‌های تجاری و محل اتصال لایه‌هاست. |
| `ArtMarketplace.Identity` | Login، ASP.NET Identity، OpenIddict و صدور token | احراز هویت برای همه UIها در یک محل مرکزی انجام می‌شود. |
| `PublicWeb` | رابط عمومی Next.js و BFF آن | tokenهای OAuth در browser storage نگهداری نمی‌شوند. |
| `Backoffice` | رابط Blazor برای کارکنان و BFF آن | دسترسی staff با role و MFA مرز امنیتی جدا دارد. |

جهت reference پروژه‌ها به شکل زیر است:

```text
Domain          -> بدون وابستگی به پروژه‌های دیگر
Application     -> Domain
Infrastructure  -> Application + Domain
Contracts       -> بدون وابستگی به implementation
Api             -> Application + Infrastructure + Contracts
Identity        -> Application + Domain + Infrastructure
```

نتیجه عملی: داخل Domain نباید `DbContext` تزریق کنید، configuration بخوانید، HTTP status برگردانید یا به ASP.NET وابسته شوید.

## ۲. اجزای در حال اجرا چگونه به هم متصل‌اند؟

```text
مرورگر کاربر عمومی
  -> Next.js BFF
  -> Identity برای Login با OIDC
  -> API با access token نگهداری‌شده در سرور

مرورگر کارمند
  -> Blazor Backoffice BFF
  -> Identity با OIDC و MFA اجباری
  -> API با access token نگهداری‌شده در سرور

Identity + API + BFFها
  -> PostgreSQL

API
  -> SeaweedFS با رابط S3-compatible
```

PostgreSQL منبع نهایی حقیقت است. Redis، Message Broker و Microservice اضافه نشده‌اند، چون در V1 مسئله‌ای وجود ندارد که هزینه عملیاتی آن‌ها را توجیه کند.

## ۳. تنظیمات سراسری Solution

### فایل `global.json`

این فایل SDK نسخه `10.0.401` را pin می‌کند و ابتدا مسیر `.tools/dotnet10` را بررسی می‌کند.

هدف: همه توسعه‌دهنده‌ها و CI با compiler/runtime یکسان build بگیرند. خطای قبلی شما با متن `Unable to resolve the .NET SDK version` دقیقاً از این مرحله می‌آمد: SDK مورد انتظار قابل پیدا کردن نبود.

برای تشخیص:

```powershell
dotnet --version
dotnet --info
dotnet build ArtMarketplace.sln -c Release
```

اگر نسخه `10.0.401` resolve نشد، اول `global.json`، سپس `.tools/dotnet10` و بعد SDKهای نصب‌شده سیستم را بررسی کنید؛ هنوز وارد debugging کد پروژه نشوید.

### فایل `Directory.Build.props`

- `Nullable=enable`: احتمال null باید در typeها مشخص باشد.
- `TreatWarningsAsErrors=true`: warning حل‌نشده اجازه ورود به build سالم ندارد.
- `AnalysisLevel=latest-recommended`: analyzerهای جدید فعال‌اند.
- `Deterministic=true`: خروجی build قابل تکرار است.
- `RestorePackagesWithLockFile=true`: dependencyها با lock file کنترل می‌شوند.

هدف: کیفیت همه پروژه‌ها از یک محل اعمال شود و پروژه جدید نتواند تصادفی استاندارد ضعیف‌تری داشته باشد.

### فایل `Directory.Packages.props`

نسخه packageهای NuGet در این فایل به‌صورت مرکزی مدیریت می‌شود. داخل `.csproj`ها معمولاً فقط نام package نوشته می‌شود.

هدف: نسخه‌های EF Core، ASP.NET Core، OpenIddict و Npgsql بین پروژه‌ها ناسازگار نشوند.

## ۴. لایه Domain؛ محل حقیقت کسب‌وکار

### `EntityId`

یک wrapper برای `Guid` است و متد `New()` دارد.

هدف: شناسه‌ها در آینده type-safeتر باشند و ساخت شناسه در کل پروژه الگوی مشخصی داشته باشد. فعلاً هنوز بسیاری از entityها مستقیماً `Guid` استفاده می‌کنند.

### `Money`

یک record تغییرناپذیر با دو مقدار `Amount` و `Currency` است. constructor مقدار منفی و currency غیر سه‌حرفی یا غیر uppercase را رد می‌کند.

چرا؟ چون object نامعتبر نباید اصلاً ساخته شود. برای پول از `decimal` استفاده شده، چون `float/double` برای مقدار دقیق مالی مناسب نیست. همچنین price نباید property خود `Artwork` باشد؛ قیمت متعلق به Listing و snapshot سفارش است.

### `UserProfile`

اطلاعات نمایشی کاربر شامل نام، locale و biography عمومی فروشنده را نگهداری می‌کند.

نکات مهم:

- setterها `private` هستند تا هر کدی نتواند state را مستقیم تغییر دهد.
- تغییر معتبر فقط از طریق `Update()` انجام می‌شود.
- trim و length validation داخل خود entity است.
- constructor بدون پارامتر private برای materialize کردن EF Core است.

هدف: فرقی نکند تغییر از API، worker یا پنل مدیریت آمده باشد؛ قانون همیشه اجرا می‌شود.

### `SavedAddress`

آدرس ذخیره‌شده کاربر است و شناسه‌ها و طول فیلدهای متنی را validate می‌کند.

هدف فعلی: مدیریت آدرس پروفایل. در Phase سفارش، آدرس باید به‌صورت immutable snapshot داخل Order کپی شود؛ Order نباید بعداً آدرس mutable پروفایل را بخواند.

### `SellerVerificationCase`

این کلاس یک **state machine** است، نه فقط چند property متناظر با ستون دیتابیس.

```text
Draft -> Pending -> Approved -> Revoked
            |          |
            |          +----> Expired
            +-> ChangesRequested -> ارسال دوباره
            +-> Rejected
```

متدهای `Submit`، `Approve`، `Reject`، `RequestChanges`، `Revoke` و `Expire` فقط transitionهای مجاز را انجام می‌دهند. `EnsureStatus()` تغییر غیرمجاز را با exception متوقف می‌کند. هر تغییر موفق `Version` را افزایش می‌دهد.

هدف:

- یک case نمی‌تواند بدون submit شدن approve شود.
- approve تکراری ممکن نیست.
- دو کارشناس نمی‌توانند ناآگاهانه تصمیم یکدیگر را overwrite کنند.
- `Version` مبنای optimistic concurrency است.

### `SellerEligibility`

تاریخچه Verification با مجوز فعلی فروش جدا شده است. این کلاس پاسخ canonical به سؤال «آیا این کاربر همین الان مجاز به فروش است؟» است.

تصمیم بر اساس موارد زیر گرفته می‌شود:

- `AccountEnabled`
- `VerificationApproved`
- `VerificationExpiresAtUtc`
- `EligibilityVersion`

متد `CanSellAt(now)` شرط اصلی است. Approve، revoke، disable یا expire باعث افزایش نسخه می‌شود.

هدف نسخه چیست؟ Listing و Order در آینده نسخه eligibility زمان publish/checkout را ذخیره می‌کنند. اگر فروشنده بعداً revoke شود، سیستم می‌فهمد snapshot قدیمی شده است؛ حتی اگر همان فروشنده دوباره approve شود.

![چرخه Verification و invalidation نسخه‌دار](../assets/art-marketplace-backend-guide-illustrations/03-seller-verification.png)

### `SellerEligibilityInvalidation`

Invalidation نسخه جدید، دلیل، کاربر و زمان را ثبت می‌کند. متد `Covers(snapshotVersion)` می‌گوید یک snapshot قدیمی تحت تأثیر این ابطال است یا نه.

Resolution به‌صورت append-only و برای target مشخص ثبت می‌شود. اگر یک ابطال چند Listing یا Order را تحت تأثیر قرار دهد، حل شدن یکی از آن‌ها نباید بقیه را خودکار پاک کند.

هدف: reapproval تاریخچه را پاک نمی‌کند و تأخیر worker نمی‌تواند پنجره فروش ناامن بسازد. طراحی در این قسمت **fail-closed** است.

## ۵. لایه Application؛ قرارداد بین منطق و تکنولوژی

### `IClock`

زمان UTC فعلی را abstract می‌کند.

هدف: تست بتواند ساعت جعلی داشته باشد. منطق expire شدن نباید به ساعت واقعی سیستم قفل شود. `SystemClock` در Infrastructure پیاده‌سازی واقعی آن است.

### `ISellerEligibilityGate`

این interface می‌پرسد آیا user مجاز به فروش است و در صورت وجود، نسخه snapshot قبلی را هم بررسی می‌کند. خروجی شامل allowed، نسخه فعلی، invalidated بودن snapshot و دلیل است.

هدف: use caseهای آینده مثل publish، checkout و payment بدانند «چه تصمیمی» لازم دارند، بدون اینکه بدانند query دیتابیس «چگونه» نوشته شده است.

### `MarketplacePolicies`

نام ثابت policyها مانند `verify-sellers` و `superadmin-with-mfa` را نگه می‌دارد.

هدف: حذف magic stringهای تکراری و typo بین API و Backoffice.

## ۶. لایه Infrastructure

### `DependencyInjection.AddInfrastructure`

این متد:

1. connection string دیتابیس را اجباری می‌کند؛
2. `MarketplaceDbContext` را با Npgsql ثبت می‌کند؛
3. implementation مربوط به clock و eligibility gate را bind می‌کند؛
4. تنظیمات object storage را هنگام startup validate می‌کند؛
5. HTTP client کوتاه‌مدت برای health check می‌سازد؛
6. liveness و readiness checkها را ثبت می‌کند.

هدف: `Api/Program.cs` فقط اعلام کند Infrastructure را اضافه کن. جزئیات ساخت objectها در یک محل باشد و config ناقص قبل از دریافت traffic باعث fail شدن startup شود.

### `MarketplaceDbContext`

از `IdentityDbContext` ارث می‌برد. بنابراین Identity، profile، verification، audit و outbox می‌توانند در یک transaction PostgreSQL ذخیره شوند.

در `OnModelCreating`:

- ابتدا `base.OnModelCreating` تنظیمات Identity را می‌سازد.
- `UseOpenIddict<Guid>()` جدول‌های OpenIddict را متصل می‌کند.
- schemaها مشخص می‌شوند: `identity`، `profiles` و `platform`.
- key، index، max length، enum conversion و concurrency tokenها تعریف می‌شوند.
- حذف cascade برای تاریخچه invalidation محدود می‌شود.

overrideهای `SaveChanges`، متد `EnforceAppendOnlyAudit()` را اجرا می‌کنند. update یا delete کردن `SecurityAuditEvent` ممنوع است.

هدف: Domain رفتار درست object را حفظ می‌کند؛ mapping دیتابیس constraint، index، concurrency و نگهداری تاریخچه را تضمین می‌کند.

### `ApplicationUser`

به user استاندارد Identity دو property اضافه می‌کند:

- `IsEnabled`: خاموش کردن فوری حساب بدون delete.
- `AuthorizationVersion`: نسخه وضعیت مجوزها برای invalidation آینده.

هدف: حذف user و اعتماد دائمی به role claim قدیمی خطرناک است. وضعیت حساب باید صریح و قابل ابطال باشد.

### `BffSession` و `PostgresTicketStore`

cookie مربوط به Backoffice فقط یک handle تصادفی دارد. ticket اصلی:

- با ASP.NET Data Protection رمز می‌شود؛
- در PostgreSQL ذخیره می‌شود؛
- با HMAC-SHA256 digest مربوط به handle پیدا می‌شود؛
- با `Surface` از sessionهای دیگر جدا می‌شود؛
- expiry و revoke دارد؛
- برای update هم‌زمان version دارد.

اگر decrypt شکست بخورد، session revoke می‌شود.

هدف امنیتی:

- سرقت دیتابیس، ticket plaintext نمی‌دهد.
- digest دیتابیس برای ساخت cookie کافی نیست.
- cookie مرورگر حاوی access/refresh token نیست.

### `SellerEligibilityGate`

رکورد فعلی eligibility را با `AsNoTracking` می‌خواند، invalidation پوشش‌دهنده نسخه قبلی را بررسی می‌کند و expiry را با `TimeProvider` می‌سنجد.

اگر eligibility وجود نداشته باشد نتیجه deny است. هدف این است که تصمیم حساس امنیتی هنگام ابهام fail-closed باشد.

### `SecurityAuditEvent`

actor، subject، نوع رخداد، داده JSON، trace ID و زمان UTC را ثبت می‌کند.

هدف: تصمیم حساس مدیریتی قابل انتساب و پیگیری باشد، بدون اینکه token یا PII اضافی در log ذخیره شود.

### `OutboxMessage`

پیام outbox در همان transaction تغییر تجاری ذخیره می‌شود. `PublishedAtUtc` و `Attempts` برای dispatcher آینده هستند.

هدف: بین commit دیتابیس و ارسال notification/event داده از دست نرود. توجه کنید که dispatcher هنوز پیاده‌سازی نشده؛ فعلاً پیام‌ها فقط durable ذخیره می‌شوند.

### Health Checkها

- `/health/live`: آیا process زنده است؟
- `/health/ready`: آیا PostgreSQL و object storage قابل دسترسی‌اند؟

هدف: process زنده ولی بدون dependency نباید traffic واقعی بگیرد.

## ۷. سرویس Identity، بخش‌به‌بخش

### تنظیم Identity

در `Identity/Program.cs` confirmed email، unique email، password قوی حداقل ۱۲ کاراکتر و lockout بعد از ۵ تلاش ناموفق برای ۱۵ دقیقه تنظیم شده است.

هدف: این کنترل‌ها باید server-side باشند. validation داخل UI فقط تجربه کاربری است و مرز امنیتی محسوب نمی‌شود.

### Cookie مربوط به Identity

نام cookie با `__Host-` شروع می‌شود، Domain ندارد، Path برابر `/`، `HttpOnly` و `Secure=Always` است، `SameSite=Lax` دارد و بعد از ۳۰ دقیقه ثابت منقضی می‌شود.

هدف: cookie بین subdomainها share نشود، JavaScript نتواند آن را بخواند و HTTP ساده باعث downgrade امنیت نشود.

### Certificateها

`CertificateLoader` فایل‌های signing و encryption را اجباری می‌کند، private key را بررسی می‌کند و در صورت مشکل startup را متوقف می‌کند.

هدف: سرویس deploy‌شده نباید بی‌صدا از key موقت development استفاده کند.

### تنظیم OpenIddict

endpointهای authorize، token، logout، introspection، revoke و userinfo فعال‌اند. فقط Authorization Code و Refresh Token مجاز است و PKCE اجباری است. access token ده دقیقه و refresh token چهارده روز عمر دارد. هر دو reference token هستند.

چرا؟

- PKCE از دزدیده شدن authorization code محافظت می‌کند.
- implicit flow حذف شده تا token داخل URL مرورگر نیاید.
- reference token مرکزی قابل revoke و introspect است.
- عمر کوتاه access token میزان خسارت سرقت را کم می‌کند.

### ترتیب Middleware

```text
HTTPS / static / localization
-> routing
-> rate limiting
-> authentication
-> authorization
-> controllers و Razor Pages
```

ترتیب مهم است. اگر Authorization قبل از Authentication اجرا شود، user ساخته نشده و نتیجه‌های 401/403 اشتباه خواهید دید.

### `AuthorizationController`

متد `AuthorizeEndpoint()`:

1. درخواست OIDC را می‌خواند؛
2. اگر cookie ورود معتبر نیست challenge می‌دهد؛
3. user را دوباره از دیتابیس می‌خواند؛
4. disabled بودن و امکان sign-in را بررسی می‌کند؛
5. role پایه `User` را در صورت نیاز اضافه می‌کند؛
6. برای roleهای staff وجود MFA را اجباری می‌کند؛
7. principal جدید می‌سازد و authorization response را صادر می‌کند.

متد `Exchange()` برای authorization code و refresh token است. user و security stamp دوباره validate می‌شوند. بنابراین تغییر password یا وضعیت امنیتی می‌تواند grant قدیمی را بی‌اعتبار کند.

`CreatePrincipalAsync()` claimها را آگاهانه از نو می‌سازد: `sub`، name، email، email verification، authorization version، زمان authentication، `acr` و `amr`.

هدف: principal قدیمی بدون بررسی کپی نشود؛ وضعیت فعلی دیتابیس تعیین کند چه claimی وارد token جدید می‌شود.

### `IdentitySeedService`

Roleها، دو BFF client و API introspection client را idempotent می‌سازد. هیچ admin یا password پیش‌فرض ایجاد نمی‌کند. فقط حساب موجودی را SuperAdmin می‌کند که email تأییدشده و MFA فعال داشته باشد.

هدف: deployment تکرارپذیر باشد، اما credential ناامن پیش‌فرض تولید نشود.

نکته: اگر client قبلاً seed شده باشد، تغییر appsettings رکورد موجود را update نمی‌کند. rotation/update باید عملیات صریح داشته باشد.

### Worker مربوط به Expiration

`SellerVerificationExpirationService` یک‌بار هنگام startup و سپس هر ساعت اجرا می‌شود. caseهای منقضی را Expired می‌کند، eligibility را invalidate می‌کند، audit و outbox می‌نویسد و همه را با یک `SaveChangesAsync` ذخیره می‌کند.

هدف: تغییر business state، audit و پیام downstream با هم commit شوند. برای scale بیشتر، batching و row claiming قوی‌تر لازم خواهد شد.

## ۸. سرویس API، بخش‌به‌بخش

### Logging و Error Handling

Serilog خروجی JSON فشرده تولید می‌کند و property مربوط به نام سرویس را اضافه می‌کند. `ProblemDetails` و `UseExceptionHandler` پاسخ خطای یکنواخت می‌دهند.

هدف: logها بر اساس trace، status و service قابل جست‌وجو باشند و stack trace داخلی به client نشت نکند.

### اعتبارسنجی Reference Token

API issuer، audience و introspection credential را تنظیم می‌کند. چون access token opaque است، API برای فهمیدن اعتبار و claimهای آن به Identity introspection می‌زند.

هدف: revoke مرکزی امکان‌پذیر است و API یک token ناشناخته را صرفاً parse و trust نمی‌کند.

### Authorization Policyها

- `StaffWithMfa`: user معتبر + role کارمندی + `amr=mfa`
- `VerifySellers`: موارد بالا + scope مربوط به API
- `SuperAdminWithMfa`: role دقیق SuperAdmin + MFA

Authentication می‌گوید «چه کسی هستی؟» و Authorization می‌گوید «اجازه انجام این کار را داری؟» بررسی role فقط در UI کافی نیست.

### Rate Limiting

به‌صورت پیش‌فرض ۱۰۰ درخواست در ۶۰ ثانیه مجاز است و queue ندارد. health و OpenAPI از limiter معاف‌اند.

هدف: burst با status 429 رد شود، ولی probe عملیاتی همیشه قابل مشاهده باشد. این فقط baseline است و هنوز policy نهایی per-user/per-IP نیست.

### Endpointهای Phase 2

- `/api/system/status`: وضعیت عمومی سرویس.
- `/api/account/me`: claimهای user فعلی.
- `/api/account/profile`: خواندن و ویرایش پروفایل خود کاربر.
- `/api/account/addresses`: فهرست، افزودن و حذف آدرس خود کاربر.
- `/api/seller-verification/submit`: ارسال درخواست فروشندگی.
- `/api/backoffice/seller-verifications/pending`: صف بررسی برای staff مجاز.
- `/api/backoffice/seller-verifications/{id}/decision`: تصمیم کارشناس.
- endpoint مربوط به invalidation resolution: ثبت حل append-only برای target مشخص.

الگوی کلی endpointها:

1. user ID از claim استاندارد `sub` استخراج می‌شود.
2. فقط داده‌ای load می‌شود که user حق دسترسی به آن را دارد.
3. constructor یا method دامنه صدا زده می‌شود.
4. خطای قابل انتظار به 400 یا 409 تبدیل می‌شود.
5. در صورت نیاز audit و outbox اضافه می‌شود.
6. یک بار save انجام می‌شود.
7. status code هدفمند برمی‌گردد.

در حذف آدرس، query هم `addressId` و هم `userId` را شرط می‌کند. این دفاع در برابر IDOR است؛ دانستن شناسه آدرس شخص دیگر برای حذف آن کافی نیست.

در تصمیم verification، `ExpectedVersion` با نسخه فعلی مقایسه می‌شود. reviewer با داده stale پاسخ 409 می‌گیرد و باید reload کند.

## ۹. BFF و دلیل امنیتی آن

![Tokenها داخل BFF باقی می‌مانند](../assets/art-marketplace-backend-guide-illustrations/02-bff-security.png)

BFF یعنی **Backend for Frontend**. مرورگر به endpoint هم‌مبدأ خودش درخواست می‌دهد. BFF tokenهای OAuth را نگهداری می‌کند و از طرف browser با API صحبت می‌کند.

### PublicWeb با Next.js

`config.ts` همه environment variableهای ضروری را validate می‌کند. کلید AES باید دقیقاً ۳۲ byte و HMAC secret حداقل ۳۲ byte باشد.

`oidc.ts`:

- metadata را از Identity discovery می‌گیرد؛
- `state`، `nonce` و PKCE verifier تصادفی می‌سازد؛
- return URL را به مسیر local محدود می‌کند؛
- code را server-side exchange می‌کند؛
- signature، issuer، audience و nonce مربوط به ID token را بررسی می‌کند؛
- access token را server-side refresh می‌کند.

`session-store.ts`:

- handle تصادفی می‌سازد؛
- فقط HMAC digest آن را در PostgreSQL نگه می‌دارد؛
- payload را با AES-256-GCM رمز می‌کند؛
- هنگام مصرف login transaction از transaction و `FOR UPDATE` استفاده می‌کند؛
- session خراب را revoke می‌کند.

مسیرها:

- `login`: شروع cross-site مشکوک را رد می‌کند، state موقت می‌سازد و به Identity می‌رود.
- `callback`: transaction را یک‌بار مصرف می‌کند، state را constant-time مقایسه می‌کند و session اصلی را می‌سازد.
- `session`: فقط اطلاعات امن user و CSRF token را می‌دهد؛ OAuth token را برنمی‌گرداند.
- `bff/[...path]`: session و CSRF را چک می‌کند، token نزدیک expiry را refresh می‌کند و درخواست را به API proxy می‌کند.
- `logout`: origin و CSRF را بررسی، session را revoke و cookie را پاک می‌کند.

هدف: XSS نتواند access/refresh token را از `localStorage` بردارد و CSRF نتواند mutation مبتنی بر cookie را اجرا کند.

### Backoffice با Blazor

از OIDC handler استاندارد ASP.NET با PKCE استفاده می‌کند. Tokenها داخل ticket سمت سرور ذخیره می‌شوند و cookie فقط handle مربوط به `PostgresTicketStore` است. fallback policy کل برنامه را به staff role و MFA محدود می‌کند.

نکته فعلی: route واقعی `/auth/login` در `Program.cs` وجود دارد، اما فایل `Login.razor` هنوز button غیرفعال «به‌زودی» نشان می‌دهد. backend آماده است ولی UI این صفحه باید در مرحله بعد به route وصل شود.

## ۱۰. دیتابیس، Migration، Audit و Outbox

Migrationها در `Infrastructure/Persistence/Migrations` هستند، چون persistence مسئولیت Infrastructure است.

schemaها مالکیت را مشخص می‌کنند:

- `identity`: user، role، OpenIddict، BFF session و security audit
- `profiles`: profile، address، verification و eligibility
- `platform`: outbox

Migration تاریخچه code-reviewed دیتابیس است. migration اعمال‌شده را برای تغییر production ویرایش نکنید؛ migration جدید بسازید.

```powershell
dotnet ef migrations add MeaningfulName `
  --project src/Backend/ArtMarketplace.Infrastructure `
  --startup-project src/Backend/ArtMarketplace.Identity

dotnet ef database update `
  --project src/Backend/ArtMarketplace.Infrastructure `
  --startup-project src/Backend/ArtMarketplace.Identity
```

Integration test مربوط به PostgreSQL یک container واقعی نسخه 17.6 بالا می‌آورد و migrationها را اعمال می‌کند. هدف کشف مشکل SQL واقعی است که provider حافظه‌ای پنهان می‌کند.

## ۱۱. تست‌ها چه چیزی را محافظت می‌کنند؟

| مجموعه تست | هدف |
|---|---|
| `Domain.Tests` | invariant پول، transitionهای verification، invalidation نسخه‌دار و جلوگیری از resolution تکراری |
| `Application.Tests` | قراردادهای ساده Application؛ فعلاً کوچک است |
| `ArchitectureTests` | جلوگیری از dependency اشتباه بین لایه‌ها |
| `AuthorizationBoundaryTests` | بدون login پاسخ 401 و بدون مجوز پاسخ 403 |
| `PostgreSqlMigrationSmokeTests` | اعمال migration روی PostgreSQL واقعی |
| `Backoffice.Tests` | baseline مربوط به assembly/configuration |
| Vitest در PublicWeb | رفتار پایه frontend |

تفاوت مهم:

- `401 Unauthorized`: سیستم user معتبر نساخته؛ مشکل authentication است.
- `403 Forbidden`: user شناخته شده ولی role، scope، MFA یا policy لازم را ندارد.

## ۱۲. روش عیب‌یابی

![عیب‌یابی را از علامت مشاهده‌شده شروع کنید](../assets/art-marketplace-backend-guide-illustrations/04-troubleshooting.png)

از boundary خراب شروع کنید و به سمت داخل بروید؛ Domain را تصادفی تغییر ندهید.

| علامت | اولین محل بررسی | علت‌های معمول |
|---|---|---|
| خطای SDK | `global.json`، `dotnet --info`، `.tools/dotnet10` | SDK 10.0.401 پیدا نمی‌شود |
| warning باعث fail شدن build | `Directory.Build.props` و diagnostic | warning عمداً error است |
| Identity در startup می‌ریزد | appsettings و `CertificateLoader` | connection string یا PFX ناقص/غلط |
| خطای redirect URI | تنظیم client در Identity و callback در BFF | اختلاف scheme، host یا path |
| cookie ذخیره نمی‌شود | HTTPS و hostname | cookie از نوع `Secure=Always` است |
| callback پاسخ 400 می‌دهد | login transaction و callback route | state/nonce غلط، cookie منقضی یا مصرف‌شده |
| API پاسخ 401 می‌دهد | token، issuer و introspection | token منقضی، Identity قطع یا audience غلط |
| API پاسخ 403 می‌دهد | policy و claimها | role، scope یا `amr=mfa` موجود نیست |
| verification پاسخ 409 می‌دهد | status و Version پرونده | transition غیرمجاز یا `ExpectedVersion` قدیمی |
| پاسخ 429 | rate limiter | سقف fixed window پر شده است |
| live سالم ولی ready خراب | متن readiness response | PostgreSQL یا SeaweedFS قطع است |
| session ناگهان 401 می‌شود | `identity.bff_sessions` و keyها | expire/revoke یا mismatch کلید رمزگذاری |
| concurrency exception | token نسخه و درخواست هم‌زمان | دو actor یک رکورد را تغییر داده‌اند |
| outbox دائماً بزرگ می‌شود | وضعیت dispatcher | فعلاً dispatcher پیاده نشده است |
| migration smoke اجرا نمی‌شود | Docker/Testcontainers | Docker daemon موجود نیست |

روال پیشنهادی:

1. URL، method، status و زمان یک request خراب را ثبت کنید.
2. مشخص کنید پاسخ را PublicWeb، Backoffice، Identity یا API داده است.
3. log همان process و trace را دنبال کنید. token، OTP و PII را log نکنید.
4. برای 401/403 اول claim و policy را بررسی کنید.
5. برای 400/409 invariant و version مورد انتظار را بررسی کنید.
6. برای 500 ابتدا startup config و readiness، سپس exception را ببینید.
7. مشکل را با کوچک‌ترین تست مرتبط بازتولید کنید.
8. fix را در لایه مالک انجام دهید و regression test اضافه کنید.

## ۱۳. روش امن اضافه کردن Feature جدید

برای مثال «ساخت Artwork Draft»:

1. قانون را با زبان ساده بنویسید.
2. رفتار و transition را در Domain مدل کنید.
3. تست Domain برای مسیر درست و رد شدن‌های مهم بنویسید.
4. use case و portهای لازم را در Application تعریف کنید.
5. EF mapping، adapter، index و migration را در Infrastructure بسازید.
6. DTO حمل‌ونقل را تعریف کنید؛ entity دیتابیس را مستقیم return نکنید.
7. endpoint را بسازید: authenticate، authorize، use case، error mapping، audit/outbox.
8. OpenAPI و client typeهای generated را به‌روز کنید.
9. تست‌های 401، 403، ownership/IDOR، stale version و PostgreSQL واقعی را اضافه کنید.
10. config، secret، health check و محدودیت‌ها را مستند کنید.

راهنمای محل قرارگیری کد:

```text
قانون یا transition کسب‌وکار؟       Domain
use case، orchestration یا port؟    Application
EF/PostgreSQL/S3/email؟             Infrastructure
HTTP، status code یا DTO؟           API / Contracts
login و صدور token؟                 Identity
session مرورگر و proxy؟             PublicWeb / Backoffice BFF
```

## ۱۴. محدودیت‌های فعلی

- فقط Foundation و Identity/Profile/Verification پیاده‌سازی شده‌اند.
- Docker در آخرین محیط validation موجود نبود؛ migration smoke واقعی باید در Docker یا CI اجرا شود.
- Identity به certificate واقعی و routing روی HTTPS نیاز دارد.
- Email confirmation/recovery قبل از production به email sender واقعی نیاز دارد.
- outbox ذخیره می‌شود ولی dispatcher هنوز وجود ندارد.
- Object Storage فعلاً config و health check دارد؛ upload/quarantine/scan مربوط به Phase 3 است.
- UI صفحه ورود Backoffice هنوز placeholder است.
- rate limiting API فعلاً baseline عمومی است.
- نوع مدرک و retention مربوط به verification هنوز تصمیم Product/Legal می‌خواهد.

این موارد مرز Phase فعلی هستند، نه قابلیت‌های پنهانی که باید موجود فرض شوند.

## ۱۵. ترتیب پیشنهادی مطالعه کد

1. `Domain/Common/Money.cs` برای ساده‌ترین invariant.
2. `SellerVerificationCase.cs` و تست آن برای state machine.
3. `SellerEligibility.cs` و تست آن برای versioned security.
4. `MarketplaceDbContext.cs` برای تبدیل مدل به PostgreSQL.
5. `Phase2Endpoints.cs` برای مسیر HTTP تا Domain.
6. `Identity/Program.cs` برای پیکربندی authentication server.
7. `AuthorizationController.cs` برای claim و token issuance.
8. routeهای login/callback/proxy در PublicWeb برای مسیر کامل browser تا API.
9. `Backoffice/Program.cs` و `PostgresTicketStore.cs` برای نسخه .NET الگوی BFF.
10. Architecture و Integration testها برای فهم guardrailها.

وقتی بتوانید مسیر کامل «ارسال و تأیید درخواست فروشندگی» را از cookie و claim تا endpoint، transition دامنه، transaction دیتابیس، audit و outbox توضیح دهید، ستون فقرات این پروژه را یاد گرفته‌اید.

