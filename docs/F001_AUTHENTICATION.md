# F001 — Authentication Integration (.NET port)

Status: Complete

## Objective and authority

Provide React authentication screens backed by ASP.NET Core 8, using existing Laravel users in the same MariaDB database. Preserve current approved behavior rather than temporary historical F001 behavior. This is a separate port record; the completed Laravel feature specification is unchanged.

After foundation completion and F001 analysis, the user explicitly approved implementation, architecture, and the listed packages with “ok proceed.” The existing untracked foundation is protected completed work, not F001-created work; only the F001 changes below are claimed.

## Findings

- Current routes provide login/logout, forgotten-password and reset-password flows, password confirmation/change, and optional email verification. Public registration routes were removed in F002; current registration tests require GET/POST /register to return 404.
- Laravel redirects successful login to /admin/dashboard and logout to /. Existing admin routes also enforce admin.access; that RBAC implementation belongs to the separate F002 port.
- User schema: unsigned bigint id, name, unique email, nullable email_verified_at, password, nullable remember_token, and timestamps. No ASP.NET Identity schema or concurrency column exists.
- Installed Laravel hashing defaults to bcrypt; local BCRYPT_ROUNDS is 12. Live password hashes have not been inspected. Compatibility must be proved with synthetic PHP/.NET fixtures before authenticating shared users or changing passwords.
- Laravel sessions use its database driver with a configured 120-minute lifetime. Laravel session payloads and cookie encryption are not ASP.NET session formats.
- Reset broker: 60-minute expiry and 60-second issuance throttle; password_reset_tokens stores one hashed token per email. Creating another Laravel reset token replaces the previous one. Sharing that table for two independent issuers would couple their pending links.
- Password defaults require at least eight characters without mandatory digit/symbol/case rules. Current views include Remember me.
- Email-verification routes/tests exist, but User does not implement MustVerifyEmail and admin routes do not require verified middleware. Do not introduce mandatory verification.
- Local mail uses Laravel's log mailer. The .NET port must not log reset/verification tokens; no external mail-provider setup is approved.

## Approved decisions

1. The user explicitly approved .NET-specific reset and verification links using framework token protection, with private development-only email files. Password changes affect shared user records; pending Laravel links remain separately managed. Links do not need to work interchangeably between the applications.
2. The user explicitly approved a dedicated local database named dss_enterprise_cms_dotnet_test with only required test tables for authentication integration tests. No destructive test setup may target dss_enterprise_cms.

Subsequent implementation approval authorized the F001 plan, custom-store architecture, and listed packages. Final validation, completion, F002, production email, and application schema changes remain unauthorized.

## Proposed scope

- React login with email, password, Remember me, error/loading states, and local return-path handling.
- Separate ASP.NET protected authentication cookie, current-user endpoint, logout, and server-enforced guest/authenticated access. No bearer tokens in browser storage.
- A minimal signed-in confirmation page at /admin/dashboard to verify authentication only. No CMS data, navigation shell, dashboard metrics, or administrative actions until F002–F004 are approved.
- Forgot-password, reset-password, password confirmation/change, and optional email-verification screens/actions.
- Registration stays unavailable; no user creation or provisioning.
- The public / foundation page and /api/health continue to work.
- Authentication tests exercise actual HTTP middleware and the approved isolated MariaDB schema. No tests create users, reset passwords, or seed records in the shared application database.

## Proposed architecture

Use ASP.NET Core's built-in cookie authentication, antiforgery services, Data Protection, rate limiting, and Identity token-provider services. A focused custom user-store adapter maps the existing users table; it must not create standard Identity tables. Use parameterized MySqlConnector commands rather than adding EF Core migrations or a general repository layer. Thin authentication endpoints delegate meaningful authentication/password operations to scoped services.

Use BCrypt.Net-Next in standard bcrypt mode for PHP-compatible verification and new password hashes; no enhanced prehashing or automatic conversion to ASP.NET's default password format. Prove PHP $2y$ verification and .NET-to-PHP verification, including non-ASCII inputs and bcrypt length behavior. Existing login passwords must not be rejected by newly invented password-policy rules. Unexpected hash algorithms fail safely and require a reported compatibility decision.

Data-protected .NET links use separate reset/verification purposes, explicit 60-minute expiry, user identity/email binding, and a password-derived security stamp so old .NET reset links become invalid after a password change. The store must re-read current values and reject stale concurrent updates; it must not overwrite unrelated user fields. Persist Data Protection keys privately through standard framework facilities; never reuse Laravel's APP_KEY or expose keys in source.

Cookie validation rechecks current user/password state so a Laravel-side password change or user deletion invalidates the .NET session. Separate cookies do not implement SSO or synchronized logout. No Laravel session rows are read or written. Reset updates password, remember_token, and updated_at in one atomic conditional statement. Neither generating nor consuming .NET links deletes/overwrites Laravel reset-token rows, preserving the approved independent-link contract. Outstanding Laravel reset links follow Laravel's existing validity/expiry; no cross-application global reset revocation is claimed.

Development email files are private, ignored, outside frontend/public/static assets, and enabled only in Development. Tokens must not appear in ordinary application/request logs or API responses. Outside Development, missing approved mail delivery fails closed with a safe operational error; no live provider or email account is connected in F001.

## Routes and UI

Proposed API routes:

- GET /api/auth/csrf — get an antiforgery request token, Cache-Control no-store.
- GET /api/auth/me — safe authenticated identity only; 401 for guests.
- POST /api/auth/login and /api/auth/logout.
- POST /api/auth/forgot-password and /api/auth/reset-password.
- POST /api/auth/confirm-password and PUT /api/auth/password.
- POST /api/auth/verification-notification and POST /api/auth/verify-email.
- GET /api/admin/dashboard — authentication-only confirmation with no CMS data during F001.

Proposed React URLs: /login, /forgot-password, /reset-password, /confirm-password, /change-password, /verify-email, /admin/dashboard, and the existing /. Ordinary links and Vite's SPA fallback are sufficient; no routing package is proposed. Unknown paths, including /register, show an unavailable page; registration API routes are not registered. The backend must reject registration requests regardless of frontend behavior.

API authentication failures return 401/403 rather than HTML redirects. The frontend redirects guests to login, preserving only approved same-origin relative paths; successful login defaults to /admin/dashboard and logout returns to /. Obtain fresh antiforgery tokens after authentication changes. Sensitive responses use no-store; reset/verification pages use a restrictive referrer policy and do not load third-party resources.

## Security and validation

- All state-changing requests, including login and anonymous recovery, require valid server-checked antiforgery tokens.
- HttpOnly, application-specific, SameSite cookies; Secure outside loopback Development. No permissive CORS.
- Match the existing 120-minute ordinary session setting. Installed Laravel SessionGuard defines rememberDuration as 576000 minutes (400 days); preserve that explicit remember-me duration unless the user approves a policy change. No project override was found in the inspected authentication setup. Cookies without Remember me remain browser-session cookies with the ordinary ticket timeout.
- Login rate limiting preserves the observed five failed attempts per email/IP per minute, with additional bounded request handling where necessary. Recovery issuance is throttled to once per email per minute; verification resend follows the current six-per-minute route limit. Single-process in-memory throttling is appropriate to local F001; multi-instance coordination needs a separately approved deployment plan.
- Server-side email/password/confirmation checks, safe error messages, parameterized SQL, and explicit user-field mapping. Recovery responses do not reveal whether an account exists.
- Password changes require the current password; password confirmation remains valid for at most the existing three-hour setting. New passwords preserve the established policy and PHP hashing compatibility.
- Verification requires an authenticated matching user and valid, unexpired, email-bound token. Repeated verification is safe; verification is optional for sign-in/admin confirmation.
- Use optimistic comparison or appropriate locking for password writes and token consumption so two .NET requests cannot consume the same reset twice. Do not claim cross-application atomic token guarantees without proving compatibility with Laravel's existing implementation.

## Database impact

No application schema changes, migrations, automatic initialization, or seeding. Application writes are limited to explicit approved authentication actions against the current user's authentication fields. Password/email-verification changes become visible to Laravel because the users table is shared. No CMS content, roles, permissions, or Laravel session payloads are modified.

Test-schema creation and test-record cleanup are limited to the explicitly approved isolated database and must guard its exact name. Shared-database credentials remain server-side; configure them without copying or printing .env. Do not run an application-password reset merely as a smoke test.

## Packages proposed for separate approval

- MySqlConnector: MariaDB ADO.NET access. Laravel already has its PHP database layer; it cannot be reused inside .NET, and ASP.NET has no built-in MariaDB driver. Official documentation lists MariaDB 10.x compatibility. Adds one maintained external runtime dependency.
- BCrypt.Net-Next: standard bcrypt support for existing PHP hashes. Laravel already supplies bcrypt in PHP; ASP.NET Identity's default password hasher uses a different format. This security-sensitive dependency requires compatibility tests and normal advisory monitoring.
- Microsoft.AspNetCore.Mvc.Testing (8.x), Microsoft.NET.Test.Sdk, xunit, and xunit.runner.visualstudio: .NET HTTP integration testing and discovery. Existing PHPUnit tests cannot execute the .NET API. Keep test-only packages in the test project; pin compatible stable versions and record transitive dependencies.

No frontend dependency is proposed. Cookie authentication, antiforgery, Data Protection, Identity token-provider services, and rate limiting come from the ASP.NET Core shared framework. No EF Core, Dapper, JWT, Identity database provider, SMTP library, or production email SDK is proposed.

## Expected files

New backend Authentication/ files for models/contracts, endpoints, services, the focused shared-user store, bcrypt adapter, cookie validation, and development mail delivery; new tests/Diwakar.Cms.Auth.Tests/ project and isolated database fixture; React auth form/layout components, API helper, and authentication pages under frontend/src/. Exact small-file grouping is an implementation detail within this scope.

Modify backend/Program.cs, backend/Diwakar.Cms.Api.csproj, non-secret backend settings, frontend/src/App.tsx and styles, local .gitignore, README.md, this specification, and SHARED_DATABASE.md as required. Parent Laravel code/governance and the completed foundation record remain protected. Do not modify the new AGENTS.md as part of ordinary F001 work.

## Proposed implementation plan

1. Resolve pending decisions, confirm the authentication contract, approve the custom-store architecture and listed packages, and privately configure shared/test database access.
2. Add the focused user-store/bcrypt adapters, framework cookie authentication, antiforgery protection, rate limiting, and current-user/login/logout endpoints.
3. Implement framework-protected reset and verification links, private development email delivery, password confirmation/change, and concurrent-update safeguards.
4. Add React authentication screens and the minimal protected sign-in confirmation; preserve the public foundation and disabled registration.
5. Add/run focused HTTP/database/security and PHP/.NET hash-compatibility tests against synthetic data only; build the frontend and present the implementation for review.

Complexity: Large. This is a cohesive authentication port with schema compatibility and security tests; no automatic feature split or implementation of F002 is authorized. If the user prefers smaller milestones, propose their exact scope for approval before changing this plan.

## Acceptance criteria

- [x] Synthetic existing PHP bcrypt credentials authenticate in .NET; new .NET hashes verify in PHP.
- [x] Invalid login, malformed input, and SQL-injection strings are safely rejected.
- [x] Login throttling works; failure responses disclose no account/password details.
- [x] Cookies, antiforgery validation, logout, and remember-me behavior match the approved contract.
- [x] Guest admin/current-user API requests are denied; minimal authenticated confirmation exposes no CMS data.
- [x] User deletion/password changes made through either application invalidate stale .NET authentication as specified.
- [x] Reset links expire, reject tampering/wrong-user/reuse, and password changes remain Laravel-compatible.
- [x] Password confirmation/change require valid authentication and the current password where applicable.
- [x] Optional email verification binds the correct user/email and rejects tampered/expired links.
- [x] Registration remains unavailable server-side; no users are provisioned.
- [x] Private development email works without logging tokens or exposing files over HTTP.
- [x] Shared schema, roles, permissions, and Laravel sessions remain unchanged; tests use only the isolated database.
- [x] Relevant focused tests pass; implementation review, final validation, and completion approvals are recorded separately.

## Analysis sources

Local sources: parent F001/F002 specifications; routes/auth.php and admin.php; User model and initial user migration; authentication controllers and LoginRequest; current Auth tests and relevant dashboard access tests; auth/session configuration; installed Laravel hashing, password broker, and validation code; shared-database foundation rules; installed ASP.NET Core 8 reference documentation.

Public primary references consulted only for the .NET compatibility decision:

- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-8.0
- https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-8.0
- https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-8.0
- https://mysqlconnector.net/
- https://www.nuget.org/packages/MySqlConnector/
- https://www.nuget.org/packages/BCrypt.Net-Next

## Implementation and validation record

### Functionality and files

Implemented the approved authentication scope with framework cookies/antiforgery/Identity tokens, a narrow parameterized MariaDB adapter, bcrypt compatibility, private local mail, React forms, and an authentication-only signed-in confirmation. Request bodies are bounded to 16 KiB; all auth writes require antiforgery validation. No RBAC or later CMS feature was introduced.

Created 21 feature files:

- Eight backend/Authentication files: AuthConfiguration.cs, AuthEndpoints.cs, AuthService.cs, AuthThrottle.cs, CmsUser.cs, DevelopmentMail.cs, PasswordHasher.cs, SharedUserStore.cs.
- backend/appsettings.Local.example.json and backend/packages.lock.json.
- Diwakar.Cms.sln and this feature specification (initially created during analysis).
- frontend/src/auth/AuthApp.tsx, frontend/src/auth/api.ts, frontend/tests/auth.test.mjs.
- tests/Diwakar.Cms.Auth.Tests: project file, AuthFixture.cs, AuthenticationTests.cs, PasswordCompatibilityTests.cs, packages.lock.json, php-bcrypt.php.

Modified 11 existing foundation files: .gitignore, README.md, backend/Program.cs, backend/Diwakar.Cms.Api.csproj, backend/appsettings.json, frontend/index.html, frontend/package.json, frontend/src/App.tsx, frontend/src/styles.css, frontend/vite.config.mjs, docs/SHARED_DATABASE.md. The remaining seven original foundation files, including AGENTS.md and FOUNDATION.md, were not modified for F001. No parent Laravel source/governance changes or Git mutations.

Packages installed and locked: MySqlConnector 2.6.2, BCrypt.Net-Next 4.2.0; test-only Microsoft.AspNetCore.Mvc.Testing 8.0.33, Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.5. No frontend package added or upgraded.

### Database and configuration

No application migrations, schema changes, application seeding, or shared user writes during agent testing. The approved test database and three minimal test tables were created and exercised. Test hosts replace MySqlDataSource explicitly with a guarded connection to dss_enterprise_cms_dotnet_test. A synthetic browser account was created only there. The local XAMPP MariaDB process is running for development.

An ignored backend/appsettings.Local.json contains only the existing database connection fields, imported privately without printing credentials. Private keys/mail, temporary local setup helpers, build outputs, and the review screenshot are ignored. MSBuild item inspection confirms private settings and .local artifacts are excluded from backend content/source items. No production email/account connection was made.

### Focused checks executed

- .NET PasswordCompatibilityTests: 4 passed, including ordinary/Unicode/over-72-byte PHP bcrypt round trips and malformed/unsupported input handling.
- .NET AuthenticationTests: 20 passed, 0 failed/skipped. Covers HTTP authorization, disabled registration, valid/invalid login, missing/tampered/stale CSRF, cookie flags and exact lifetimes, confirmation expiry, password changes, external password-change/deletion invalidation, recovery throttling, tampered/expired/wrong-account/reused links, concurrent reset consumption, verification/resend limits, and fail-closed production email/Secure cookies.
- The recovery test was then improved to consume the actual delivered .eml URL; that single focused test passed again, confirming the encoded link works and Laravel-style reset/session rows remain untouched. There are 24 unique .NET test cases, not 25.
- npm run test:auth: 2 passed, validating safe return destinations, fresh CSRF headers, same-origin credentials, and stopping writes when CSRF acquisition fails.
- npm run build: passed with strict TypeScript checks and production bundling after the final frontend changes.
- Browser on the isolated test database: login form, synthetic account sign-in, signed-in confirmation, logout to /, and guest redirection back to login verified. Narrow login layout visually checked; screenshot saved privately for review. No real shared account was used.
- API private local paths: unavailable; Vite backend settings path returns 403, and private directory URLs expose no files (SPA-shell fallback only). Backend project content/source item checks exclude all private artifacts.

The initial sandbox test-runner connection restriction and stopped MariaDB service were resolved by running approved local checks outside the network sandbox and starting local MariaDB. Focused tests exposed a cookie clock mismatch, which was corrected by deriving issuance and expiry from one TimeProvider value. No assertion or security control was disabled to make tests pass.

### Assumptions, limitations, and review gate

- Shared application schema is assumed to match the supplied Laravel migrations. Tests verified that mapping against an isolated MariaDB schema; no live shared password hashes or application data were inspected.
- Email delivery is private Development pickup only. Production delivery remains intentionally unavailable pending a separate approved setup.
- Cookies are separate between applications. Laravel's existing sessions and pending reset links follow Laravel's rules; see the independently managed reset limitation above.
- In-memory throttling is for a single process. Multi-instance coordination, production TLS/hosting/key provisioning, RBAC, and the real dashboard remain outside F001.
- Node 24.1 emits its native TypeScript-stripping experimental warning during the two frontend tests; tests pass, and the production Vite build does not use that test-only loading path.
- Implementation review: approved by the user instruction to use the new folder and proceed after the review report.
- Final validation: passed after explicit authorization; relocation and final results are recorded below.
- Final completion: explicitly approved by the user on 2026-10-28 after the final validation report.

## Final validation and relocation — 2026-10-28

Active project root: C:\Ashish\Projects\DiwakarEnterpriseCMS\DMS_React_Net_Core_8.
The user moved the project into this standalone local repository and instructed the agent to use the new path and proceed. Historical references to react-dotnet-cms above describe the original implementation location. The completed foundation record and governance files were preserved.

### Validation results

- Locked .NET restore succeeded from the new folder. Release compilation succeeded with no source changes or new dependencies.
- The first Release test run passed all four PHP/.NET compatibility cases, while 20 database-dependent cases could not connect because MariaDB was stopped. After starting the existing local XAMPP service, only AuthenticationTests were rerun: 20 passed, 0 failed/skipped. Final total: all 24 unique .NET tests passed across these runs.
- npm run test:auth: 2 passed. The previously documented Node native TypeScript-stripping warning remains test-only.
- npm run build: passed, including strict type checking and the Vite production bundle.
- Relocated HTTP smoke checks passed: direct/proxied health HTTP 200 Healthy; guest current-user/admin endpoints HTTP 401; login HTML HTTP 200; backend private settings blocked by the Vite server.
- Both development servers run from the new project root, using the normal private application configuration. No browser login, password reset, or other mutation was performed against shared application users during final validation.

### Final reviews

- Security/validation/authorization: reviewed cookie flags/lifetimes, password-stamp checks, server-side antiforgery on writes, parameterized SQL, conditional password writes, safe errors, rate limiting, token purpose/expiry/binding, return-path restrictions, output escaping, and private development mail. Existing tests cover the relevant positive and negative paths. No unresolved finding within the approved local F001 scope.
- Database/migrations: no application migrations, schema changes, seeds, or shared-user writes. All test mutations remain guarded to dss_enterprise_cms_dotnet_test. Laravel sessions/reset-token records follow the previously approved independent lifecycle.
- Dependencies/build artifacts: lockfiles unchanged; no extra dependency or upgrade. All 39 tracked files were inventoried; no tracked private settings, .local files, generated output, node_modules, or .env files. MSBuild content/source item checks exclude private settings/mail/keys/helpers.
- Final source/diff/regression review: the moved repository started clean at the user's existing local commit. Existing implementation and foundation files were treated as protected work. Authentication source, tests, configuration, and relevant frontend code were reviewed against approved requirements. Only this specification and the README were changed during final validation. No debug statements, unexplained TODOs, raw HTML injection, permissive CORS, automatic migrations, accidental routes, or governance edits found.
- PHP/Laravel tests and Pint were not run: the standalone .NET project contains no changed Laravel application code. PHP was used only for synthetic bcrypt compatibility fixtures. No architecture change or ADR was required beyond the approved F001 design.

Functional acceptance: 12/12 functional criteria verified. The combined workflow criterion is satisfied following explicit final user approval; all 13 acceptance checklist items are satisfied.
Known limitations remain unchanged: development email pickup only, independent Laravel sessions/reset links, single-process throttling, and production hosting/key/email setup deferred. The private mail/key defaults and test root discovery use the relocated content root automatically; no application path code change was required.

Finalization status: Complete following explicit user approval on 2026-10-28. This completion update changes only the F001 specification, preserving the existing README and specification changes. No application changes, validation reruns, database operations, commits, or remote Git access were performed for this update. F002 analysis requires separate user permission.
