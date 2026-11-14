# ADR-0002: Identity مرکزی، OIDC و sessionهای BFF

- وضعیت: Accepted در Phase 2؛ baseline خودمیزبان برای V1
- تاریخ: ۲۰۲۶-۱۰-۱۳

## زمینه

PublicWeb مبتنی بر Blazor WebAssembly و Backoffice مبتنی بر Blazor Server دو frontend مستقل‌اند و API باید مجوزدهی مرجع را انجام دهد. نگهداری access/refresh token در `localStorage` خطر XSS دارد و به همین دلیل هر رابط BFF مستقل دارد.

## تصمیم

### بخش قطعی

- قرارداد میان frontendها، Identity و API استاندارد OIDC/OAuth است و هر frontend یک BFF مستقل دارد.
- auth host مالک UI و endpointهای register/login/recovery/MFA است؛ صفحه‌های auth در Public entry/redirect/return هستند و credential را پردازش نمی‌کنند. cookie خود IdP فقط همان host را authenticate می‌کند و در business API پذیرفته نمی‌شود.
- هر دو BFF از Authorization Code + PKCE به‌عنوان confidential web client استفاده می‌کنند؛ implicit و password grant ممنوع‌اند.
- سه session مستقل IdP، Public BFF و Backoffice وجود دارد. cookie یا Data Protection key ring مشترک روی parent domain نداریم.
- cookie هر BFF فقط یک handle تصادفی opaque و پرآنتروپی است؛ این مقدار bearer با host-only، `Secure`, `HttpOnly`, `Path=/` و `SameSite=Lax` محافظت می‌شود. store فقط HMAC/hash handle را نگه می‌دارد و raw handle هرگز در DB یا log ثبت نمی‌شود.
- هر BFF یک session schema/store فنی اختصاصی با credential حداقلی دارد؛ به جدول‌های دامنه دسترسی ندارد. PostgreSQL برای V1 کافی است و Redis فعلاً توجیه ندارد. tokenهای server-side با کلید خارج از DB رمز می‌شوند. outage، timeout، lookup error یا decrypt failure باید دسترسی را fail-closed رد کند و هیچ fallback به in-memory session، cookie خودبسنده یا token مرورگر مجاز نیست.
- API cookie هیچ frontend را authenticate نمی‌کند؛ فقط Bearer با `issuer`, `audience`, `scope` و `azp/client_id` معتبر پذیرفته می‌شود. client عمومی scope مدیریتی ندارد.
- BFF یک proxy عمومی نیست: route/method/scope allow-list، destination ثابت، حذف headerهای ورودی حساس (`Authorization`, `Cookie`, `Host` و forwarding headerها)، body limit و `Cache-Control: no-store` برای داده authenticated لازم است.
- هر endpoint حساس BFF، session، antiforgery و authorization را بررسی می‌کند. وضعیت نمایش‌داده‌شده در componentهای Blazor فقط UX است و جای کنترل API را نمی‌گیرد.
- همه unsafe methodها، login و logout به anti-CSRF token متصل به session و بررسی دقیق `Origin`/Fetch Metadata نیاز دارند. callback OIDC از `state`, `nonce`, PKCE، redirect URI دقیق و session rotation استفاده می‌کند.
- MFA برای `Expert`, `Admin`, `SuperAdmin` اجباری است. Backoffice flow باید assurance لازم را درخواست کند و API `acr/amr` را کنترل کند؛ عملیات حساس SuperAdmin به `auth_time` تازه/step-up نیاز دارند.

### baseline self-hosted پیشنهادی

- ASP.NET Core Identity منبع account/credential/role/recovery/MFA و OpenIddict authorization server است.
- reference access/refresh token با TTL محدود، refresh rotation و reuse detection ترجیح دارد تا revocation مؤثر باشد. اگر JWT انتخاب شود، TTL کوتاه، `authz-version` و راهبرد block/revocation صریح لازم است؛ security stamp به‌تنهایی JWT صادرشده را باطل نمی‌کند.
- logout، disable account، password/MFA/role change و reuse detection، session/refresh tokenهای مربوط را revoke می‌کنند. endpointهای پرخطر account status و permission جاری را دوباره می‌خوانند.
- signing/encryption keys خارج از repository، با rotation، backup، access audit و owner عملیاتی نگهداری می‌شوند؛ نبود یا unreadable بودن کلید لازم باید fail-closed باشد، نه تولید خاموش کلید موقت یا session/token ناامن.

### تصمیم باز

پیش از Phase 2 باید self-hosted baseline با یک IdP مدیریت‌شده از نظر کشور، data residency، هزینه، SLA و availability مقایسه و یکی تصویب شود. قرارداد OIDC+BFF در هر دو حالت ثابت می‌ماند.

## پیامدها

### مثبت

- token از JavaScript و localStorage دور می‌ماند.
- هر frontend قابل deploy مستقل است و API trust boundary واضح دارد.
- role/policyها در Backend مرجع باقی می‌مانند.
- SSO و client isolation با پروتکل استاندارد ممکن است.

### منفی

- authorization server، session store و key lifecycle سطح امنیتی حساسی دارند.
- login/logout/callback و CSRF integration باید برای دو frontend تست end-to-end شوند.
- self-hosting OpenIddict نیازمند patching، key rotation و threat review است.

## گزینه‌های ردشده

- **JWT در localStorage:** در برابر token theft ناشی از XSS نامناسب است.
- **cookie مشترک برای PublicWeb/Backoffice/API:** coupling deployment، سطح دسترسی بیش از حد و مدیریت key مشترک ایجاد می‌کند.
- **cookie auth مستقیم cross-origin به API:** CORS/CSRF و Server Componentها را پیچیده می‌کند.

## gate پیش از Phase 2

- انتخاب و ثبت IdP نهایی
- تعیین access token format/TTL، refresh TTL/rotation/reuse behavior و revocation SLA
- تعیین idle/absolute session timeout، Blazor revalidation interval و concurrent-refresh locking
- topology دقیق hostها، `AllowedHosts`، trusted proxyها، exact originها و CORS policy
- سیاست key generation/rotation/backup و مسئول patch/recovery
- تست threatهای login CSRF، session fixation، token replay، stale privilege و logout propagation

