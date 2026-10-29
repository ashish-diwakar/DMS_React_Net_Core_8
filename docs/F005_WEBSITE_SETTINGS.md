# F005 — Website Settings (.NET port)

Status: Complete

## Source and scope

Port the completed Laravel `../dss-enterprise-cms/docs/features/F005_WEBSITE_SETTINGS.md` feature to the React and ASP.NET Core 8 application, using the same Laravel-owned database. The user authorized F005 analysis after F004 was marked Complete. The approved .NET implementation and focused checks are recorded below.

The Laravel implementation uses one structured `website_settings` table, `WebsiteSetting::SINGLETON_ID = 1`, one Settings form, and `settings.manage`. The existing Admin role does not receive this permission by default. Its fields are `site_name`, `tagline`, `public_email`, `primary_phone`, `secondary_phone`, `address`, five named social URLs (Facebook, Instagram, LinkedIn, YouTube, X/Twitter), and `footer_text`. There are no business-name, logo, favicon, SEO, or secret fields. Laravel renders missing settings without inserting a row, and creates/updates ID 1 on save.

## Current .NET state and decisions proposed

The .NET app already reads Spatie web permissions, including `settings.manage`, from the shared database. Its admin API group requires authentication, `admin.access`, CSRF verification for writes, and no-store responses. The React shell shows Settings as a disabled future-module label when permitted; `/admin/settings` is not yet a known route. The isolated .NET test database does not yet have a `website_settings` test table. The local working tree was clean before this F005 document; F004 and earlier code is baseline, not F005 work.

- Use the existing Laravel table and singleton ID 1. No .NET migration, seeder, new permission, or role grant. A missing row displays empty optional fields; the first valid save creates ID 1. Use one atomic database upsert so concurrent first saves cannot create duplicate rows. Updates to the shared settings row will be visible to Laravel.
- Use the exact existing Laravel field set. Require a nonblank site name (120 characters). Optional limits: tagline 180, email 255, each phone 50, address 1000, each social URL 2048, footer text 500. Validate public email and require HTTP or HTTPS absolute social URLs. Treat blank optional inputs as null. Keep footer text plain text and let React escape it.
- Retain one `settings.manage` permission for both viewing and updating. Super Admin bypass remains; Admin needs an explicit existing role or direct grant. The Admin default role receives no new permission.
- Expose authenticated `GET /api/admin/settings` and CSRF-protected `PATCH /api/admin/settings`; each endpoint also checks `settings.manage` server-side. Add `/admin/settings` to the existing React route set and replace the Settings placeholder with an active sidebar link only when authorized.
- Use the F003 shell, heading, spacing, form controls, and feedback patterns. Group fields as General, Contact, Social links, and Footer. Do not modify the F004 dashboard or implement public-site/theme consumption, caching, media selection, SEO, or future modules.

## Proposed implementation plan

1. Add a focused `WebsiteSettingsStore` and typed request/response contract to read and update only the approved columns of the existing singleton row. Use parameterized SQL and an atomic ID-1 upsert.
2. Add the two protected admin endpoints with server-side `settings.manage` authorization, input validation, safe error messages, and the existing CSRF/no-store behavior.
3. Add the responsive Settings form to the current React admin shell, including load, save, field validation feedback, success/error states, and authorized navigation. Preserve entered values on a failed save.
4. Extend only the isolated test fixture with the Laravel-compatible settings table, then add focused API tests for guest/unauthorized/authorized/Super Admin access, first save/update, invalid values, unapproved fields, and CSRF. Add focused frontend rendering/validation checks where useful.
5. Run focused checks and present the result for user code/UI review. Await separate approval before final validation; do not start F006.

## Expected impact and risks

Expected files: `backend/Authentication/AuthEndpoints.cs`; a new backend settings store/contract under `backend/`; `frontend/src/auth/AuthApp.tsx`, `frontend/src/auth/api.ts`, `frontend/src/admin/AdminShell.tsx`, `frontend/src/styles.css`; a new Settings form component; `tests/Diwakar.Cms.Auth.Tests/AuthFixture.cs`; new focused backend/frontend tests; this document. If the existing authorization response needs a dedicated `canManageSettings` flag, update it and its client type in the same scope.

Database impact: reads and authorized writes to Laravel's existing `website_settings` row ID 1; no shared schema change or migration. Only the dedicated test database receives a test-table definition. Package impact: none. Security: enforce `admin.access` and `settings.manage` on the API, maintain CSRF, whitelist persisted fields, reject unsafe URLs and secrets, and render text escaped. Regression risks: shared-row updates from both apps, first-save concurrency, a missing table on an installation that has not applied Laravel F005, role-permission changes during a request, and responsive form consistency. Complexity: Medium.

## Port acceptance criteria

- [x] Guest and users without both required admin permissions cannot read or write settings.
- [x] Super Admin and explicitly authorized Admin can view and save the Laravel-compatible field set.
- [x] Missing row reads without a write; first save creates ID 1; later saves update ID 1 without duplicates.
- [x] Invalid required fields, email, unsafe social URLs, and overlong values are rejected server-side.
- [x] Only approved public fields persist; no infrastructure secrets or arbitrary HTML are introduced.
- [x] Settings navigation and responsive form use the existing shell and show clear save/error feedback.
- [x] No shared migration, Admin default grant, package, media upload, SEO field, or F006 work is added.
- [x] Focused tests pass, and user review precedes final validation and completion.


## Implementation and focused-check record — 2026-10-29

The user approved the plan. The new settings store reads only Laravel's existing singleton row and writes only the twelve approved public fields with a parameterized ID-1 upsert. A missing row is returned as an empty form without a database write. Both API endpoints use the existing authenticated admin group; they require `settings.manage` server-side, while the group retains `admin.access`, CSRF on PATCH, and no-store responses. The authorization response now exposes `canManageSettings` for the React shell.

The React admin shell shows an active Settings link only for permitted accounts. The Settings form follows the shell's card, heading, spacing, input, and button styles; it groups General, Contact, Social links, and Footer fields, preserves entered data on failed saves, and shows server field errors and save feedback. The existing F001–F004 pages remain in their routes.

Files added: `backend/Settings/WebsiteSettingsStore.cs`, `frontend/src/admin/WebsiteSettingsPage.tsx`, `tests/Diwakar.Cms.Auth.Tests/WebsiteSettingsTests.cs`, and this feature document. Files changed: `backend/Authentication/AuthConfiguration.cs`, `backend/Authentication/AuthEndpoints.cs`, `frontend/src/admin/AdminShell.tsx`, `frontend/src/auth/AuthApp.tsx`, `frontend/src/auth/api.ts`, `frontend/src/styles.css`, `frontend/tests/admin-shell.test.mjs`, `frontend/tests/auth.test.mjs`, and `tests/Diwakar.Cms.Auth.Tests/AuthFixture.cs`. No shared database migration, package, Admin default grant, Laravel file, or governance file changed. The isolated test fixture creates a matching table only in `dss_enterprise_cms_dotnet_test` and clears only that test table between tests.

Focused checks: F005 .NET integration tests 5/5 passed; affected admin-shell tests 3/3 passed; affected auth client tests 3/3 passed; TypeScript/Vite production build passed. The local Debug API process at port 5088 was verified as this project, refreshed with the new build, and returned health HTTP 200. The first focused test invocation found a C# compiler issue in a validation helper; it was fixed before the passing run. No full regression suite or final validation has been run at this stage.

Assumptions and risks: the local shared application database has Laravel's F005 `website_settings` table and `settings.manage` permission; a missing table will surface as service unavailable rather than trigger a .NET schema change. The shared singleton uses last-save-wins semantics when both apps edit it concurrently. At the focused-check stage, browser review and final validation were pending. F006 and the deferred navigation improvement have not begun.


## User review correction — save confirmation

The user asked for the successful-save message to match the Laravel-style green banner and be visible after saving. The Settings form now places a full-width bordered green confirmation between the page heading and first settings card. When a save succeeds, the page scrolls the live status message into view; save errors use the same placement with error styling. The successful message remains the API's “Website settings updated.” response. The correction changed only the Settings form and its scoped CSS. The frontend TypeScript/Vite build passed after the correction; backend tests were not rerun because backend code did not change. At that stage, user review and final-validation approval were pending.


## Final validation and finalization — 2026-10-29

After the save-banner correction, the user approved final validation. The complete isolated .NET Release test suite passed (42/42). All applicable frontend tests passed (10/10), and the configured TypeScript/Vite production build passed. The .NET suite used only `dss_enterprise_cms_dotnet_test`; no shared application data was reset or seeded. An expected test-path `InvalidOperationException` warning and a Vite test-server port warning appeared, but all checks completed successfully.

Security and authorization review: the settings API remains under authenticated `/api/admin` with `admin.access`, checks `settings.manage` on both endpoints, validates CSRF on PATCH, and returns no-store responses. The store validates all approved fields, uses parameterized SQL, rejects non-HTTP(S) social links, writes only named columns, and returns no infrastructure secrets. React escapes text and renders the success or error banner in the form flow. Migration and database review: the application adds no schema change and uses only Laravel's existing singleton ID 1; the test fixture creates a matching table only in the isolated test database. Diff review found no unrelated edits, generated tracked files, new dependency, debugging code, secret, Laravel edit, or remote Git operation.

Known limit: the agent did not use a signed-in shared-database browser session during final validation; the user reviewed the UI and approved this stage. The shared installation must have Laravel's F005 table and permission. Concurrent writes from Laravel and .NET use last-save-wins behavior. The separately deferred navigation improvement remains outside F005.

The user gave final approval on 2026-10-29. F005 is Complete. F006 Media Library is next in the approved roadmap and requires separate permission before analysis begins.
