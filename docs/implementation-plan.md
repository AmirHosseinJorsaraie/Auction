# برنامه پیاده‌سازی V1 و معیارهای پذیرش

> اجرای پروژه مرحله‌ای است. پایان هر فاز با گزارش فارسی، build/test واقعی، روش بررسی دستی و توقف برای تأیید کاربر همراه است.

## قواعد مشترک همه فازها

پیش از شروع coding هر فاز، یک mini-plan فارسی برای تأیید ارائه می‌شود که هدف، **فهرست دقیق فایل‌ها/ماژول‌ها**، تصمیم‌های معماری و فرض‌های همان فاز را مشخص می‌کند. پس از اجرا، گزارش باید command و نتیجه واقعی build/test، محدودیت‌ها، خلاصه فایل‌های تغییرکرده، روش بررسی دستی و یادداشت آموزشی Next.js/TypeScript را ثبت کند؛ سپس کار متوقف می‌شود.

هر فاز فقط وقتی Done است که:

- scope همان فاز کامل و خارج از scope وارد نشده باشد.
- پروژه‌های تحت تأثیر build شوند و تست‌های مرتبط پاس شوند؛ failure مستند هرگز پنهان نشود.
- authorization سمت Backend، validation و failure pathهای حیاتی تست شوند.
- مستندات `progress.md`, `learning-notes.md`, `known-issues.md` و ADR مرتبط به‌روز شوند.
- UIهای جدید RTL، responsive و keyboard-accessible بررسی شوند.
- migration و configuration قابل تکرار باشند و secret واقعی commit نشود.
- قابلیت جدید از UI/API موردنظر قابل دسترسی باشد.
- راهنمای بررسی دستی، command/result واقعی، فایل‌های تغییرکرده و محدودیت‌های واقعی تحویل داده شود.
- همه مشکل‌های بحرانی شناخته‌شده گزارش شوند؛ هیچ مورد امنیت/یکپارچگی داده پنهان نماند.
- در هر فاز guard خودکار/بازبینی شود که هیچ schema/table/endpoint/UI/job/enum مربوط به Auction یا verification سطح ۳ ساخته نشده است.
- برای آغاز فاز بعد تأیید صریح کاربر دریافت شود.

### نقشه اولیه فایل/ماژول هر فاز

این نقشه در mini-plan ابتدای هر فاز به فایل‌های دقیق تبدیل می‌شود:

| فاز | مسیرها/ماژول‌های اصلی |
|---|---|
| 1 | root build/config، همه projectهای Backend، `Frontend/PublicWeb`, `Frontend/Backoffice`, test skeletonها، `deploy/`, CI |
| 2 | `Modules/Identity`, `Modules/Profiles`, API auth/account، Public auth/account routes، Backoffice auth/verification، integration/security tests |
| 3 | `Modules/Catalog`, Marketplace Draft، storage adapters، Public seller artwork forms و `My Artworks`، upload/IDOR tests |
| 4 | `Modules/Reviews`, audit/notification adapters، Backoffice review queue/details، workflow/concurrency tests |
| 5 | Catalog/Marketplace public queries، Public gallery/detail/category/artist و `My Listings`، SEO/UI tests |
| 6 | `Modules/Orders`, cart/checkout/reservation، Public `My Orders`/details، concurrency/expiry tests |
| 7 | `Modules/Payments`, provider contracts، Fake adapter، callback/reconciliation endpoints، idempotency tests |
| 8 | `Modules/Administration`, Backoffice dashboards/management/content/audit، shared design tokens و accessibility/RTL tests |
| 9 | E2E/security/performance suites، deployment/migration/backup runbooks و readiness docs |

## Phase 0 — Requirements & Architecture

### هدف

تثبیت scope، مرزها، مدل دامنه، راهبرد امنیت و مسیر V2 بدون نوشتن production code.

### خروجی‌ها

- `docs/architecture.md`
- `docs/domain-model.md`
- `docs/v2-readiness.md`
- `docs/implementation-plan.md`
- ADRهای اولیه در `docs/decisions/`
- `docs/progress.md`, `docs/learning-notes.md`, `docs/known-issues.md`

### معیار پذیرش

- V1 و out-of-scope V2 صریح‌اند.
- ماژول‌ها و dependency rule تعریف شده‌اند.
- ER و state machineهای اولیه وجود دارند.
- OIDC/BFF، CSRF، policy و admin hardening توضیح داده شده‌اند.
- invariant جلوگیری از double-sell و مسیر channel exclusivity V2 مشخص است.
- تصمیم‌های باز، زمان تصمیم و ریسک آن‌ها ثبت شده‌اند.

### بررسی دستی

مرور همه diagramها و تأیید شش ADR اولیه. در این فاز build کاربردی انتظار نمی‌رود، چون solution فعلی پروژه‌ای ندارد.

### توقف

Phase 0 پس از تحویل اسناد در وضعیت «در انتظار تأیید» می‌ماند. Phase 1 خودکار آغاز نمی‌شود.

---

## Phase 1 — Solution Foundation

### هدف

ایجاد اسکلت قابل build/start برای Backend، Public Web و Backoffice همراه با PostgreSQL، storage محلی، health، logging، CI و تست پایه.

### فایل‌ها و ماژول‌های برنامه‌ریزی‌شده

- `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`
- پروژه‌های `Domain`, `Application`, `Infrastructure`, `Contracts`, `Api`
- `src/Frontend/PublicWeb` با Next.js App Router/TypeScript/Tailwind/shadcn پایه
- `src/Frontend/Backoffice` با Blazor Web App / Interactive Server
- test projectهای Unit, Integration, Architecture و frontend
- `deploy/docker-compose.yml`, Dockerfileهای مستقل و `.env.example`
- PostgreSQL و یک S3-compatible local implementation انتخاب‌شده با بررسی license/provenance/patching و image digest pin‌شده
- Serilog JSON، Problem Details، OpenAPI، liveness/readiness
- baseline rate limiting middleware و policyهای پایه قابل تنظیم
- `.github/workflows/ci.yml`

### تصمیم‌های مهم قبل از اجرا

- ریشه نهایی repository و نام solution/product
- نسخه دقیق .NET 10 patch، Node، Next.js و packageها پس از compatibility spike
- ارتقای Node 20 EOL به یک خط LTS پشتیبانی‌شده و انتخاب local S3 implementation
- topology اولیه دامنه‌ها و reverse proxy
- strategy migration در startup در برابر job کنترل‌شده deployment

### معیار پذیرش

- از clone تمیز، restore/build هر سه deployable موفق است.
- API، Next.js و Blazor مستقل start می‌شوند.
- readiness واقعاً PostgreSQL و dependency ضروری را بررسی می‌کند؛ liveness به سرویس خارجی وابسته نیست.
- `docker compose config` و full stack محلی موفق‌اند.
- Architecture Tests dependencyهای Clean Architecture را enforce می‌کنند.
- Testcontainers یک migration/connection smoke test واقعی PostgreSQL اجرا می‌کند.
- config لازم fail-fast است و هیچ secret واقعی در repository نیست.
- CI شامل restore، build، typecheck/test و artifact/log قابل فهم است.
- drift میان OpenAPI و client/typeهای تولیدشده Public/Backoffice در CI تشخیص داده شود.
- baseline rate limit فعال باشد و endpoint-specific policy در فاز قابلیت مربوط تکمیل شود.
- هیچ type، route، table یا job مربوط به Auction ایجاد نشده است.

### بررسی دستی

- بازکردن OpenAPI و health endpoints
- مشاهده home حداقلی RTL در Public Web و login placeholder در Backoffice
- توقف/راه‌اندازی PostgreSQL برای دیدن readiness صحیح

### نکته آموزشی

ساختار App Router، نقش `layout.tsx`/`page.tsx`، مرز Server/Client Component و تفاوت build-time/runtime environment توضیح داده می‌شود.

---

## Phase 2 — Identity & User Profiles

### هدف

حساب واحد، OIDC/BFF session، role/policy، profile، verification فروشنده و دسترسی امن Backoffice.

### scope پیاده‌سازی

- ASP.NET Core Identity و OpenIddict یا IdP جایگزینِ تأییدشده
- Authorization Code + PKCE برای Next.js و Blazor
- sessionهای server-side مستقل و cookieهای host-only
- auth-host UI برای register/login/logout/email confirmation/recovery/MFA و entry/return routeهای Public
- reference token یا راهبرد JWT کوتاه‌عمر مصوب، rotation/reuse detection/revocation و key lifecycle
- صفحات account، profile، saved address و `SellerVerificationCase`
- role، verification status و permission policyهای جدا
- summary canonical و versionدار `SellerEligibility`، durable append-only invalidation/outbox و resolution ممیزی‌شده
- MFA اجباری staff، lockout/rate limiting و audit امنیتی اولیه
- anti-CSRF برای mutationهای BFF/cookie-based
- endpoint/UI حداقلی seller onboarding و Backoffice verification

### معیار پذیرش

- register/login/logout end-to-end برای Public و login/MFA برای Backoffice کار کند.
- هیچ access/refresh token در DOM، Web Storage یا cookie قابل خواندن JavaScript وجود نداشته باشد.
- cookie عمومی در Admin/API و cookie Admin در Public/API قابل استفاده نباشد.
- تست `Set-Cookie` ثابت کند هر cookie نشست `Secure`, `HttpOnly`, `SameSite=Lax`, `Path=/` و بدون `Domain` است؛ نام/namespace و store/credential نشست Public و Backoffice مستقل باشند.
- session id پس از login موفق، recovery و step-up/تغییر privilege چرخانده شود و مقدار قبلی قابل reuse نباشد.
- sessionهای server-side PostgreSQL پس از restart سرویس معتبرِ مورد انتظار بمانند و یک تست دو replica خواندن/ابطال مشترک را ثابت کند.
- نبود یا unreadable بودن signing/encryption key باعث fail-closed شدن startup/flow شود و هیچ کلید موقت ناخواسته ساخته نشود.
- cookie فقط raw handle تصادفی پرآنتروپی را حمل کند؛ DB فقط HMAC/hash آن را داشته باشد و raw handle در DB/log پیدا نشود. outage/timeout/lookup/decrypt failure باید بدون fallback به in-memory یا cookie خودبسنده دسترسی را fail-closed رد کند.
- درخواست mutation با session معتبر ولی CSRF نامعتبر رد شود.
- API فقط token با issuer/audience/scope معتبر را بپذیرد.
- `azp/client_id`, `acr/amr` و برای step-up، تازگی `auth_time` مطابق policy کنترل شود.
- `User`, role، verification و capability با هم ادغام نشده باشند.
- کاربر unverified نتواند seller use case اجرا کند.
- transitionهای verification، expiry/revocation و ایجاد پرونده جدید بعد از rejection با تست پوشش داده شوند.
- disable account یا expire/revoke verification، `SellerEligibility` را اتمیک invalid کند، sessionهای لازم را revoke کند و همه use caseهای فروش بلافاصله نسخه جاری را enforce کنند؛ تأخیر cleanup نباید پنجره فروش بسازد.
- توالی سریع `valid(v1) → revoked(v2) → valid(v3)` نباید invalidation v2 را حذف کند؛ work item پس از crash/restart باقی بماند و snapshotهای v1 تا resolution صریح fail-closed باشند.
- `SellerEligibilityInvalidation.InvalidatedVersion` و resolution target-specific append-only با actor/reason/evidence اتمیک و auditable باشند؛ reapproval هرگز invalidation قبلی را processed/resolved نکند و replay worker idempotent باشد.
- contact requirement اولیه `CanCheckout` و failureهای recovery/MFA تعریف و تست شوند.
- پاسخ 401/403 و resource authorization با integration test پوشش داده شود.
- role/MFA change، disable و refresh reuse، session/tokenهای لازم را در SLA مصوب revoke کند.
- seed role idempotent و bootstrap اولین SuperAdmin بدون password پیش‌فرض باشد.

### بررسی دستی

- ثبت‌نام و ورود/خروج در Public
- ارسال verification و مشاهده وضعیت
- ورود staff با TOTP و بررسی منوهای permission-based
- تست دستی CSRF و session expiration در محیط توسعه کنترل‌شده

### نکته آموزشی

BFF، OIDC code flow، cookie در برابر token، policy-based authorization و تفاوت validation UI با authorization سرور توضیح داده می‌شود.

---

## Phase 3 — Catalog & Artwork Submission

### هدف

ساخت Artwork/Revision، draft، metadata، media و مدارک خصوصی و submission immutable برای review.

### scope پیاده‌سازی

- Artwork، ArtworkRevision، Artist و Category پایه
- صفحات `My Artworks` و `Submit Artwork` و draft/resume/edit با ownership/capability check زیر lock مشترک
- media metadata، upload intent تک‌مصرف، quarantine/finalize و promotion بایت دقیق اسکن‌شده به clean key server-only/create-if-absent، محدودیت MIME/signature/size و malware scan fail-closed
- quota/rate limit برای upload intent، finalize و download خصوصی
- public derivatives و private documents با access policy
- provenance/ownership references
- freeze revision و submit اتمیک برای review، با یک Draft mutable/ReviewCase non-terminal/round pending در هر Artwork
- `MarketplaceListing` فقط در حالت Draft برای نگهداری price/currency پیش از submission؛ publication تا approval ممنوع

### معیار پذیرش

- فقط seller تأییدشده و مالک بتواند draft ایجاد/ویرایش کند.
- create/edit/submit revision باید `User/SellerEligibility → Artwork → ReviewCase` را به ترتیب بگیرد، owner/capability و `CreatedByOwnerUserId` را زیر lock revalidate کند؛ pre-read برای authorization کافی نیست.
- partial unique indexها حداکثر یک Draft mutable برای Artwork، یک ReviewCase non-terminal برای Artwork و یک round `PendingReview` برای case را enforce کنند؛ دو submit هم‌زمان دقیقاً یک freeze/case/round بسازند و بازنده بدون side effect conflict بگیرد.
- Create Draft باید `User/SellerEligibility → Artwork` را lock، ownership/capability را زیر lock revalidate و سپس با unique constraint insert کند؛ race مالک قبلی با `Sold → Completed` نباید Draft stale بسازد.
- revision ارسال‌شده دیگر قابل mutation نباشد؛ تغییر، revision جدید بسازد.
- فایل با extension/MIME/signature/size نامعتبر رد شود.
- فایل تا نتیجه `Clean` قابل مصرف/دانلود نباشد؛ scanner timeout/unavailable و malware نتیجه fail-closed بدهند.
- finalize همان stream/version اسکن‌شده را با SHA-256 به clean key جدید و غیرقابل overwrite promotion کند؛ asset/revision فقط clean key + hash (+ `VersionId` در صورت پشتیبانی) را pin کند و هرگز quarantine key را مرجع نگیرد.
- تست overwrite هم‌زمان quarantine پس از upload/هنگام scan ثابت کند فقط بایت‌های واقعاً اسکن‌شده قابل `Clean` شدن‌اند؛ refetch mutable، اعتماد صرف به ETag و public derivative از ورودی خام ممنوع باشد.
- فایل quarantine یا private public URL دائمی نداشته باشد و public ACL ممنوع باشد.
- تست IDOR برای Artwork و document خصوصی پاس شود.
- submit شامل transition شرطی Draft→Frozen و ایجاد/ادامه review case/round یکتا به‌صورت اتمیک باشد.
- race edit/submit با `Sold → Completed` دو نتیجه امن داشته باشد: اگر edit/submit اول commit شد transfer همان transaction cleanup آن را terminal/withdraw کند؛ اگر transfer اول commit شد command به‌علت owner mismatch رد شود. Draft/review قبلی هرگز به خریدار واگذار نشود.
- object orphan و failure بین upload/finalize policy مستند و تست شود.
- سقف تعداد فایل، cleanup قابل تکرار و re-encode/metadata stripping تصویر تست شود.
- Listing Draft قیمت مثبت، currency/settlement unit پیکربندی‌شده و ownership معتبر داشته باشد.
- `PublishedArtworkRevisionId` در Draft تهی و غیرقابل‌تنظیم توسط seller باشد؛ هنوز هیچ ادعای approval/publication ایجاد نکند.
- `Artwork` هیچ price/listing/auction status نداشته باشد.
- migration هیچ table/column مربوط به Auction نداشته باشد.

### بررسی دستی

ثبت اثر فارسی، upload تصویر و PDF، مشاهده draft، submit و اطمینان از قفل ویرایش؛ تلاش کاربر دیگر برای دسترسی به فایل خصوصی باید رد شود.

### نکته آموزشی

React form composition، React Hook Form، Zod inference، presigned upload و تفاوت client validation با domain invariant توضیح داده می‌شود.

---

## Phase 4 — Expert Review & Moderation

### هدف

صف بررسی کارشناسی با decisionهای auditable، تغییرخواهی و حفاظت concurrency.

### scope پیاده‌سازی

- review queue/detail و assignment policy
- finding/evidence، approve، reject و request changes
- history append-only و optimistic concurrency
- re-review policy برای revision جدید
- annotation غیرماهوی و `ReviewDecisionSupersession` ماهوی با effective-decision rule و audit
- notification پس از commit بدون message broker
- audit actor/time/action/target/reason

### معیار پذیرش

- فقط دارنده `Reviews.Decide` با assignment/resource policy بتواند تصمیم دهد؛ mapping پیش‌فرض آن Expert است و Admin صرفاً با نام role این اختیار را ندارد.
- Expert نتواند اثر خود یا مورد دارای conflict-of-interest را review کند.
- دو تصمیم نهایی رقیب فقط یک برنده داشته باشند.
- transition نامعتبر و decision بدون reason لازم رد شوند.
- seller فقط نتیجه و comment مجاز را ببیند، نه note خصوصی.
- approval دقیقاً revision دیده‌شده را معتبر کند.
- supersession نتیجه قبلی را حذف/ویرایش یا case بسته را reopen نکند؛ supersession/effective-status/safeguard تجاری را اتمیک ثبت و remediation را به case فعال attach کند یا case/round جدید بسازد.
- race supersession با submit جدید باید، مستقل از ترتیب commit، یک supersession و safeguard پایدار و حداکثر یک case/round فعال بسازد؛ unique/serialization conflict با retry idempotent به workflow برنده attach شود و Listing دارای approval superseded هرگز قابل publish/checkout نماند.
- تغییر trust-sensitive باعث revision/review جدید شود.
- audit و notification idempotent باشند؛ شکست notification decision را rollback نکند.

### بررسی دستی

دو مرورگر Expert/Seller برای approve، reject، changes request و resubmit؛ ایجاد race کنترل‌شده میان دو Expert.

### نکته آموزشی

optimistic concurrency، immutable history و cache invalidation در TanStack Query توضیح داده می‌شود.

---

## Phase 5 — Public Gallery & Marketplace Listings

### هدف

ارائه تجربه عمومی premium و RTL و انتشار listing قیمت ثابت با ruleهای قطعی.

### scope پیاده‌سازی

- Home، gallery، category، artwork detail، artist، About، Contact
- search/filter/sort/pagination با query contract پایدار
- publish/withdraw/suspend/reinstate listing و revision-pin swap فقط از policy متمرکز `CanPublishListingNow`
- اتصال supersession: Draft غیرقابل publish بماند و Published در همان transaction به Suspended برود
- اتصال seller/account invalidation: Published به Suspended، Draft غیرقابل publish و projection عمومی فوراً غیرقابل خرید شود
- صفحه `My Listings` برای seller با status و عملیات مجاز
- availability، seller public information و قیمت format‌شده
- metadata، sitemap/canonical/OpenGraph پایه
- loading، empty، error و responsive states

### معیار پذیرش

- فقط seller معتبر + owner + approved revision بتواند publish کند.
- publish در یک transaction تصمیم مؤثر review را revalidate، `PublishedArtworkRevisionId` را به revision دقیق pin و `Draft → Published` را انجام دهد؛ state فعال بدون pin رد شود.
- publish/reinstate/release/revision swap همگی owner، account، `SellerEligibility`, approval مؤثر، hold و channel constraint را زیر guard revalidate کنند؛ swap در Reserved/Sold رد شود.
- publish/reinstate نسخه eligibility معتبر را روی Listing snapshot کنند؛ version mismatch یا invalidation حل‌نشده re-publish را رد کند.
- checkout حق refresh ضمنی `SellerEligibilityVersionAtPublish` را نداشته باشد؛ فقط resolution ممیزی‌شده + reinstate/re-publish می‌تواند snapshot Listing را به نسخه جاری ببرد.
- race reinstate یا revision swap با supersession/eligibility invalidation هرگز Listing قابل‌خرید با approval/capability نامعتبر تولید نکند.
- listingهای Draft/Suspended/Withdrawn از API عمومی حذف شوند؛ Sold/Completed فقط با snapshot تأییدشده و indicator غیرقابل‌خرید نمایش داده شوند.
- partial unique index بیش از یک listing باز برای Artwork را رد کند.
- transitionهای متعلق به این فاز (`Draft`, `Published`, `Suspended`, `Withdrawn`) و guardهای domain-level همه stateهای V1 تست شوند؛ integration triggerهای `Reserved`, `Sold`, `Completed` در فازهای ۶ و ۷ پذیرفته می‌شوند.
- cache یا UI قدیمی هرگز listing غیرقابل‌خرید را در Backend قابل checkout نکند.
- pagination ترتیب deterministic داشته باشد و filter/sort تست شود.
- private document، object key یا PII در HTML/API عمومی ظاهر نشود.
- semantic HTML، keyboard focus، contrast و alt text بررسی شوند.
- صفحات کلیدی در mobile/tablet/desktop و RTL مرور شوند.
- Server Component برای read-heavy pageها و Client Component فقط در مرز interaction استفاده شود.

### بررسی دستی

سناریوهای gallery/search/filter/sort/paging و نمایش mixed-direction email/code؛ بررسی ۳ viewport و بدون JavaScript تا حد ممکن برای محتوای عمومی.

### نکته آموزشی

Server Components، route segments، `searchParams`، streaming/loading UI، metadata و زمان استفاده از TanStack Query توضیح داده می‌شود.

---

## Phase 6 — Cart, Checkout & Orders

### هدف

سبد تک‌فروشنده‌ای، checkout، رزرو اتمیک و سفارش با snapshotهای معتبر.

### scope پیاده‌سازی

- cart سروری برای کاربر ثبت‌شده، یک seller/یک currency
- checkout summary و تأیید تغییر قیمت
- `Order.ReservationExpiresAtUtc` یکتا با TTL قابل تنظیم و ساعت دیتابیس/clock قابل آزمون؛ Reservation فرزند deadline مستقل ندارد
- `ReviewIntegrityHold` و `SellerEligibilityHold` برای reservation/order، مستقل از expiry عادی
- rate limit/idempotency برای checkout request
- Order/OrderItem/price/address و `SellerEligibilityVersionAtCheckout` snapshot
- history/detail سفارش خریدار و view پایه فروشنده/admin
- صفحات `My Orders` و Order Details با timeline وضعیت
- shipping/tracking پایه طبق policy محصول

### معیار پذیرش

- quantity همواره ۱ و seller حق خرید listing خودش را نداشته باشد.
- `CanCheckout` باید account فعال، contactهای مصوب و shipping address کامل را enforce کند.
- cart item قیمت یا availability را تضمین نکند؛ checkout دوباره از DB بخواند.
- در N checkout موازی برای یک اثر فقط یکی reservation/order موفق بسازد.
- ایجاد Order و رزرو همه itemها اتمیک باشد؛ failure جزئی باقی نماند.
- lock چند listing با ترتیب قطعی انجام و overlapping cart/deadlock retry تست شود.
- تغییر قیمت نیازمند تأیید مجدد خریدار باشد.
- Order مبلغ/ارز/title/seller/address را snapshot کند.
- فقط Order، `SellerEligibilityVersionAtCheckout` و `ReservationExpiresAtUtc` را snapshot کند؛ همه Reservationهای اجباریِ آن این دو مقدار را از والد می‌گیرند و schema ستون موازی ندارد. تست schema/domain ثابت کند deadline یا version متفاوت برای child قابل ساخت نیست.
- checkout زیر lock برای هر Listing، valid/current بودن seller eligibility، `SellerEligibilityVersionAtPublish == SellerEligibilityVersionAtCheckout` و نبود invalidation حل‌نشده پوشاننده را بررسی کند. mismatch یک item کل checkout را بدون Order/Reservation رد و Listingهای stale را اتمیک با audit `Suspended` کند.
- تست `publish(v1) → revoke/invalidation(v2) → worker crash/lag → reapprove(v3) → checkout` باید صفر Order/Reservation بسازد؛ resolution صریح + reinstate باید Listing و Order بعدی را روی v3 pin کند.
- race checkout و revoke فقط دو outcome امن داشته باشد: checkout نخست Order v1 می‌سازد که payment بعدی block می‌شود، یا revoke نخست commit می‌شود و checkout هیچ Orderی نمی‌سازد. در cart چنداثری یک Listing stale نیز کل عملیات را رد کند.
- expiry/cancel فقط reservation متعلق را آزاد کند و idempotent باشد؛ همه candidate/lazy/payment checks فقط ساعت دیتابیس در برابر deadline یکتای Order را بخوانند و BackgroundService batch cleanup در crash/retry/multi-replica تست شود.
- expiry worker در سطح Order candidate کشف کند، نخست SellerEligibility guard و سپس همه Artwork guardهای مرتب را بگیرد و completeness را بررسی کند؛ اگر هر guard لازم `SKIP LOCKED` شد هیچ itemی mutation نشود. سپس Listing/Order revalidate و همه reservationها all-or-nothing terminal شوند.
- تست Order دواثری با یکی از Artwork guardهای قفل‌شده ثابت کند Order/listingها جزئی expire/release نمی‌شوند، هر دو دقیقاً deadline والد را دارند و retry بعدی امن است.
- تست contention روی SellerEligibility guard ثابت کند expiry پیش از Artwork/Order lock نمی‌گیرد و پس از invalidation فقط all-or-nothing به Suspended terminal می‌کند.
- reservation/order دارای `ReviewIntegrityHold` یا `SellerEligibilityHold` می‌تواند با expiry/manual cancel terminal شود، اما Listing باید `Suspended` و commercial lock/incident حفظ شود؛ auto-republish/unlock ممنوع و eligibility contract برای payment/fulfillment fail-closed است.
- race میان supersession و publish/checkout یا expiry worker با commercial guard و lock order مشترک تست شود؛ نتیجه هرگز reservation بدون hold یا listing دوباره Published نباشد.
- race supersession با cancel/manual release تست شود: اگر release زودتر commit شد، supersession باید Listing منتشرشده را Suspended کند؛ اگر supersession زودتر بود، hold باید release/re-publish را مسدود کند.
- manual cancel زیر guard باید `AwaitingPayment → Cancelled`، terminal کردن همه reservationها و transition همه listingها را در یک transaction انجام دهد؛ فقط item واجد `CanPublishListingNow` به Published و بقیه به Suspended بروند و late receipt قابل apply نباشد.
- expiry/cancel/release و reinstate در برابر seller verification expiry/revoke، account disable و ownership change race-test شوند؛ هیچ Listing فاقد owner/eligibility/approval به Published برنگردد.
- listing `Sold` تا transaction صریح انتقال مالکیت و `Completed` lock را آزاد نکند.
- دسترسی به سفارش کاربر دیگر و IDOR با تست رد شود.
- transition نامعتبر Order/Listing رد و audit شود.

### بررسی دستی

دو session خریدار برای race روی یک اثر، cart از دو seller، تغییر قیمت پس از add-to-cart و expiration رزرو با clock کنترل‌شده.

### نکته آموزشی

mutationهای optimistic در برابر transaction authoritative، idempotency UI و server cache revalidation توضیح داده می‌شود.

---

## Phase 7 — Payment Abstraction

### هدف

قرارداد provider-independent، Fake Provider فقط development و verification/callback idempotent.

### scope پیاده‌سازی

- `IPaymentProvider` و تفکیک صریح `PaymentAttempt`، `PaymentProviderEvent`، `VerifiedPaymentReceipt`، `OrderPaymentApplication` و `PaymentReconciliationCase`
- merchant reference و idempotency key
- initiation خارج از DB transaction و recovery record قبل از call
- callback signature/freshness/event deduplication و server verification contract
- Fake Provider برای Development/Test با production startup guard
- consistency/reconciliation policy پایه و `OwnershipTransfer` append-only/idempotent

### معیار پذیرش

- Production با Fake Provider fail-fast شود.
- redirect مرورگر به success هیچ Order را Paid نکند.
- amount، currency، order reference و provider account با snapshot تطبیق داده شوند.
- callback/event تکراری یک side effect بیشتر ایجاد نکند.
- provider transaction id/event id constraints یکتا باشند.
- provider outcome همه receiptهای واقعاً متفاوت را ثبت کند، اما unique constraint روی `OrderPaymentApplication(OrderId)` اجازه بیش از یک اعمال مالی به Order ندهد.
- هر `OrderPaymentApplication` با FK و constraint به همان `VerifiedPaymentReceipt`/event موفق قابل ردیابی باشد، نه صرفاً به attempt کلی.
- تست race دو success معتبر با transaction/event متفاوت روی یک Order دقیقاً دو event/receipt، یک application و یک reconciliation بسازد؛ rollback یا گم‌شدن receipt دوم مجاز نباشد.
- supersession approval در حالت Reserved پرداخت/فروش خودکار را hold کند و در حالت Sold incident و fulfillment/ownership-transfer hold بسازد؛ هیچ‌کدام state تجاری را به عقب برنگردانند.
- race supersession با callback موفق دو نتیجه مجاز دارد: یا callback زودتر commit شده و `Sold` همراه incident/hold ثبت می‌شود، یا supersession زودتر commit شده و receipt بدون application وارد reconciliation می‌شود؛ هیچ receipt یا hold گم نشود.
- `Sold → Completed` فقط زیر commercial guard، با revalidation پرداخت/مالکیت/hold و trigger حقوقی مصوب انجام شود؛ race با supersession یا انتقال را کامل و سپس incident ممیزی‌شده ثبت کند، یا hold را زودتر ثبت و انتقال را fail-closed متوقف کند.
- `OwnershipTransfer` باید unique `OrderItemId/ListingId` داشته و exact `ArtworkId`, `OrderId`, `OrderPaymentApplicationId` را pin کند؛ from = owner/listing/order seller جاری و to = order buyer باشد. retry انتقال side effect دوم نسازد.
- callback/payment application و ownership transfer در برابر account/seller eligibility invalidation race-test شوند؛ receipt همیشه ثبت شود ولی capability/hold نامعتبر application یا transfer را به reconciliation/incident ببرد.
- تست contention روی SellerEligibility در callback ثابت کند lock order معکوس نمی‌شود و اگر invalidation برنده شد، receipt commit ولی application صفر و reconciliation یک است.
- تست crash/lag با توالی `valid(v1) → revoked(v2) → valid(v3)` و callback هم‌زمان ثابت کند invalidation durable می‌ماند، receipt ثبت می‌شود، application صفر و reconciliation/hold ایجاد می‌شود.
- callback پیش از application باید deadline یکتای Order و eligibility snapshot خود Order و همه Listingهای آن را با نسخه جاری/invalidation history مقایسه کند؛ mismatch هر item یک receipt، صفر application، یک reconciliation و صفر Listing `Sold` نتیجه دهد.
- تست Order ساخته‌شده در v1 سپس revoke(v2)، crash worker و reapprove(v3) با callback موفق دقیقاً `VerifiedPaymentReceipt=1`, `OrderPaymentApplication=0`, `PaymentReconciliationCase=1` بسازد.
- race `CreateMarketplaceDraft` فروشنده قبلی با `Sold → Completed` فقط نتایج امن بدهد: پیش از transfer به‌علت listing non-final یا پس از transfer به‌علت owner mismatch رد شود؛ Draft stale هرگز insert نشود.
- انتقال مالکیت باید در همان transaction همه ArtworkRevisionهای mutable مالک قبلی را `Discarded(reason=OwnershipChanged)` و ReviewCase/round باز او را `Withdrawn(reason=OwnershipChanged)` کند؛ frozen/published history را حفظ کند و خریدار فقط با revision/submission جدید ادامه دهد. race transfer با edit/submit و retry cleanup تست شود.
- callback با signature/secret، freshness، merchant account و server verification نامعتبر رد شود؛ replay و event خارج از ترتیب تست شوند.
- race بین expiry و callback تست شود.
- late success به حالت reconciliation/refund-required مصوب برود و double-sell نسازد.
- secret، token، signature، payload حساس و PII در log ثبت نشود.
- پیش‌نیازهای provider واقعی و موارد حقوقی صریحاً مستند بمانند.

### بررسی دستی

سناریوهای fake success/failure/cancel، callback تکراری، amount mismatch و Production misconfiguration.

### نکته آموزشی

adapter/port، webhook idempotency، eventual reconciliation و دلیل جدایی OrderStatus از PaymentStatus توضیح داده می‌شود.

---

## Phase 8 — Admin Operations & UI Polish

### هدف

تکمیل عملیات Admin/SuperAdmin و یکنواختی UI/RTL/accessibility در Public و Backoffice.

### scope پیاده‌سازی

- dashboard معنی‌دار و queryهای صفحه‌بندی‌شده
- users، verification، categories، artworks/listings، orders و payment monitoring
- content محدود banner/informational pages
- moderation، suspension، audit views و role assignment SuperAdmin
- sensitive configuration محدود و allow-listed برای SuperAdmin؛ بدون نمایش secret raw
- loading/empty/error/confirm/toast در همه workflowها
- RTL، responsive، focus، keyboard و contrast audit

### معیار پذیرش

- permission matrix چهار role با تست API پوشش داده شود.
- disable/reactivate کاربر و invalidation دسترسی در Backoffice تست شود.
- Admin نتواند خود را SuperAdmin کند یا آخرین SuperAdmin حذف شود.
- عملیات حساس reason، MFA/step-up لازم و audit داشته باشند.
- Backoffice هیچ DbContext/query persistence مستقیم نداشته باشد.
- dashboard داده واقعی و pagination محدود داشته باشد.
- private document/PII فقط با least privilege دیده شود.
- صفحات و modal/table/breadcrumb/pagination در RTL و viewportهای هدف بررسی شوند.
- WCAG checks خودکار و مرور keyboard دستی بدون blocker بحرانی باشند.
- sensitive configuration به allow-list محدود باشد و secret raw در UI/API نمایش داده نشود.

### بررسی دستی

اجرای permission matrix با حساب‌های Expert/Admin/SuperAdmin، مرور کامل keyboard-only و snapshotهای responsive RTL.

### نکته آموزشی

component composition، design tokens، accessible primitives و تفاوت authorization UI با Backend توضیح داده می‌شود.

---

## Phase 9 — Hardening & Delivery

### هدف

اثبات workflowهای حیاتی، امنیت، concurrency، عملیات deployment و readiness واقعی V1.

### scope پیاده‌سازی

- E2E seller → review → publish → checkout → verified payment
- integration/concurrency/security regression tests
- rate limit و abuse/upload/configuration review
- performance baseline و query/index analysis
- deployment، migration/rollback، backup/restore و observability runbook
- production config/secret/TLS/forwarded-header review
- readiness checklist و به‌روزرسانی V2 readiness

### معیار پذیرش

- E2E بحرانی در محیط غیرProduction ولی production-like پاس شود؛ Fake Provider هرگز در Production فعال نمی‌شود.
- double-sell، review race، callback duplicate و expiry race در CI پایدار باشند.
- regression raceهای supersession با publish/checkout، expiry worker و payment callback قاعده commercial guard/lock order را اثبات کنند؛ deadlock/serialization retry محدود و idempotent نیز fault-injection شود.
- regression supersession case تاریخی هم‌زمان با submit workflow جدید ثابت کند case بسته reopen نمی‌شود، فقط یک workflow فعال باقی می‌ماند و safeguard حتی زیر unique conflict گم نمی‌شود.
- regression raceهای cancel/release و `Sold → Completed` با supersession/checkout/callback ثابت کنند Listing بدون approval مؤثر re-publish و مالکیت در حضور hold منتقل نمی‌شود.
- regression invalidation seller/account با publish/reinstate/revision-swap/expiry/cancel/payment/transfer ثابت کند policy canonical هیچ پنجره فروش یا انتقال ایجاد نمی‌کند.
- regression end-to-end `publish(v1) → revoke(v2) → restart/worker lag → reapprove(v3)` هم checkout جدید را بدون resolution رد کند و هم callback Order قدیمی را فقط به receipt/reconciliation ببرد؛ سپس resolution + reinstate ممیزی‌شده تنها مسیر checkout v3 باشد.
- migration روی DB خالی و نسخه قبلی آزمایش و rollback/forward-fix plan ثبت شود.
- restore drill واقعی با RPO/RTO توافق‌شده انجام شود.
- dependency/security scan بدون vulnerability بحرانی حل‌نشده باشد.
- load baseline با حجم توافق‌شده و بودجه latency/query ثبت شود.
- secretها از secret manager و TLS/cookie/headerها به‌درستی تنظیم شوند.
- runbook incident، backup، key rotation و payment reconciliation وجود داشته باشد.
- known critical security/data-integrity issue باز نماند.
- اعلام «production-ready» فقط پس از انتخاب/تأیید provider واقعی، بررسی حقوقی و آزمون عملیاتی انجام شود.

### بررسی دستی

تمرین deployment و rollback/forward-fix، restore، key rotation و incident simulation همراه با ثبت شواهد.

### نکته آموزشی

test pyramid، contract/E2E tradeoff، migration safety، RPO/RTO و observability مبتنی بر trace توضیح داده می‌شود.

## ترتیب تصمیم‌های لازم

| قبل از فاز | تصمیم الزامی |
|---|---|
| 1 | ریشه/نام solution، hosting baseline، نسخه‌های toolchain |
| 2 | self-hosted یا managed IdP، domain topology، MFA/recovery staff |
| 3 | verification fields/retention، file limits/types/provider، Artist policy |
| 4 | review assignment/conflict، تعریف تغییر substantive و notification channel |
| 5 | category taxonomy، public seller fields، locale/currency display |
| 6 | cart policy نهایی، reservation TTL، shipping، cancellation/ownership transfer |
| 7 | کشور/provider/currency، late payment/refund/reconciliation policy |
| 8 | permission matrix نهایی، audit retention و content governance |
| 9 | hosting، SLO، RPO/RTO، compliance و production payment approval |
