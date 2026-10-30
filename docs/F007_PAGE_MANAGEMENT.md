# F007 — Page Management (.NET port)

Status: Complete

## Approved scope and Laravel reference

The user authorized F007 implementation using the supplied Pages design, with action buttons fitted to desktop and mobile widths. The supplied Page Sections screen is a visual reference for F008 and is not implemented in F007. New reusable React UI components belong in frontend/src/components/pages; earlier feature components remain in place for a separately approved cleanup.

This port uses Laravel's existing pages table: unsigned id, title varchar(200), unique slug varchar(150), status varchar(20) default draft, and timestamps. Page identity/lifecycle only; content, public rendering, sections editor, hierarchy, homepage, templates, menus, SEO, and media selection remain outside F007. No shared migration or Page seeder is added.

Only pages.manage grants Page administration, in addition to admin.access. Super Admin retains the existing authorization bypass. The Admin baseline is unchanged. The .NET API enforces permission and CSRF on writes; hiding navigation is only a UI affordance.

Slug behavior follows the Laravel feature for common ASCII/Latin text: create derives a safe lowercase single-segment slug when omitted, manual edits are normalized, existing slugs remain stable on title-only updates, reserved names are rejected, and the database unique index is authoritative. Without a transliteration package, a title made entirely of non-Latin characters may require a manually entered ASCII slug. This is a known cross-app difference for user review; no dependency was added.

Deletion is permanent only after explicit confirmation. The API locks the Page row and checks page_sections before deleting. The existing Laravel foreign key restricts concurrent section references as a second safeguard. A referenced Page receives a conflict response and is retained. No Page Sections data is created by this feature.

## Implementation

- backend/Pages/PageValidation.cs, PageStore.cs, PageEndpoints.cs: validation, parameterized browse/CRUD, server authorization, unique-slug conflict response, and protected deletion.
- backend/Authentication/AuthConfiguration.cs and AuthEndpoints.cs: Page store DI and admin-route registration.
- frontend/src/admin/pages/PageIndex.tsx and PageEditor.tsx: permission-aware list, filters, pagination, create/edit, validation feedback, and save redirects.
- frontend/src/components/pages/: reusable PageForm, PageTable, PageStatusBadge, PageDeleteDialog, types, and client slug suggestion.
- frontend/src/admin/AdminShell.tsx, frontend/src/auth/AuthApp.tsx, frontend/src/auth/api.ts, and frontend/src/styles.css: sidebar/routing/safe return paths and the supplied admin layout. Index buttons are Edit/Delete only in F007 and fit in a wrapping action row; Manage Sections arrives in F008.
- tests/Diwakar.Cms.Auth.Tests/AuthFixture.cs and PageTests.cs: only the guarded test database gains the pages table and nullable page_sections.page_id reference. The shared application schema is untouched.
- frontend/tests/pages.test.mjs and admin-shell.test.mjs: focused component, navigation, and path checks.

No package, lockfile, Laravel source, governance file, shared-schema migration, or shared data was changed.

## Focused verification — 2026-10-29

- Isolated .NET Page and directly affected Media tests: 14 passed, 0 failed.
- Focused frontend Page and admin-shell tests: 10 passed, 0 failed.
- Frontend production build and typecheck: passed.
- Backend build: passed. The older local API executable initially locked the normal output path; a separate-output build passed, then that verified process was restarted after a normal build. The health route returned 200 and anonymous GET /api/admin/pages returned 401.
- Manual signed-in desktop/mobile visual review: pending user review.

Potential remaining risks: non-Latin automatic slug transliteration differs from Laravel without an extra package; concurrent edits to the same Page use last-save-wins behavior; the current navigation still performs full-page loads as previously deferred by the user. Final validation and completion require later explicit approvals.


## Final validation — 2026-10-30

The user approved proceeding after implementation review. The full isolated .NET regression run passed 56/56 tests, with zero failures, and wrote a local TRX report. All 19 frontend regression tests passed. The frontend production build and TypeScript typecheck passed. A final route review found that PATCH on a missing Page could report a duplicate slug before 404; the route now checks Page existence first, and all six focused Page tests passed after that correction. The local API was rebuilt and restarted; health returned 200 and anonymous GET /api/admin/pages returned 401.

Security and validation review: all Page API operations require the existing authenticated admin group plus pages.manage; writes use the existing CSRF filter. Server validation restricts title, slug, and Draft/Published status, ignores unapproved JSON fields, enforces the reserved names, and relies on the existing unique slug index for races. SQL values are parameterized and React escapes displayed Page text. Delete requires an explicit native confirmation dialog, checks page_sections under a row lock, and retains the Laravel foreign-key safeguard.

Database and migration review: no application migration, Page seeder, or shared schema change. Only the guarded dss_enterprise_cms_dotnet_test fixture gained Page structures and is cleaned within that database. No package or lockfile change. Existing F001–F006 functionality remained in the full regression suite. No F008 Page Sections editor, public renderer, dashboard metric, or unrelated refactor was introduced. Final diff inspection found no secrets, temporary debug code, generated build artifacts, or unexplained TODOs in the change set. No architecture-document or ADR update is needed for this approved shared-table port.

The UI follows the user-supplied Pages design and keeps action buttons within narrow layouts. The user approved proceeding after review; agent-operated signed-in viewport screenshots were not captured. The approved non-Latin slug-transliteration limitation and current full-page navigation behavior remain documented.

## Acceptance criteria

- [x] Existing Laravel Page identity/lifecycle fields are read and written without a new shared migration.
- [x] Super Admin and explicitly granted pages.manage users can manage Pages; default Admin and guests cannot.
- [x] Create and edit preserve the approved title, slug, status, reservation, and uniqueness rules.
- [x] Search, status filtering, deterministic ordering, and 20-item pagination work.
- [x] Confirmed deletion blocks Pages with sections and retains their content.
- [x] React list and forms use the current admin shell, responsive action layout, and new Components folder.
- [x] Focused tests, full isolated regression tests, frontend tests, typecheck, build, security, and diff reviews pass.
- [x] F008 content/sections editing and public rendering remain deferred.

Final user approval was given on 2026-10-30. F007 is Complete. F008 analysis has not begun.

## Completion approval — 2026-10-30

The user explicitly approved marking F007 Complete. This status update changes only the feature record and README; no application code, database data, schema, dependencies, or tests were changed or rerun. F008 Page Sections is next in the approved roadmap and requires separate permission before analysis.
