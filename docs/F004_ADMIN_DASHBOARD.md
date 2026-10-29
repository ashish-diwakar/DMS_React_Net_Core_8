# F004 — Admin Dashboard (.NET port)

Status: Ready for Final Approval

## Source and order

Port the completed Laravel `../dss-enterprise-cms/docs/features/F004_ADMIN_DASHBOARD.md` requirements to this React and ASP.NET Core 8 project after F003 Admin Application Shell is Complete and the user authorizes F004 analysis. Continue the approved order: F004 Dashboard, F005 Website Settings, F006 Media Library, F007 Page Management, F008 Page Sections. This note records a user-approved dashboard requirement; it does not approve F004 implementation or any later feature.

The Laravel F004 baseline calls for one protected `/admin/dashboard` in the F003 shell; authentication and `admin.access`; current implemented CMS data only; permission-aware summaries and quick links; efficient count queries; responsive presentation; and no future-module statistics, new dependency, database migration, or analytics system. Preserve F001 account actions and F002 authorization.

## Approved dashboard behavior — 2026-10-29

- Super Admin sees the existing global CMS totals for users, web roles, and web permissions, with authorized links to implemented areas.
- Admin sees information useful to that signed-in account: the assigned primary role, the count or clear summary of effective permissions (including existing direct grants), and links to currently implemented sections that the account may access. This is role/permission-aware content, not a user-configurable dashboard layout.
- Show the global CMS user total to an Admin only when `users.view` permits it, and label it clearly as the **total CMS user count**. Show Users and Roles quick links only with their existing view permissions. Do not expose global role/permission catalog counts to an Admin merely because `admin.access` allows the dashboard.
- Do not present a “users belonging to this Admin” count: the current schema has no user-owner or tenant relationship. Do not introduce one for F004.
- Enforce data visibility on the .NET server for the current authenticated account. Client-side hiding alone is insufficient. Keep the existing protected dashboard route and Super Admin bypass behavior.

## Analysis — 2026-10-29

The user authorized F004 analysis after F003 was marked Complete. The original Laravel F004 dashboard uses the F003 shell, `admin.access`, permission-aware Users/Roles/Permissions summary cards and quick links, aggregate count queries, and no future-module data. Its existing controller shows global role/permission totals to an Admin with `roles.view`. The user-approved .NET variation above instead gives Admin their own primary role and effective permission count. This explicit later decision takes precedence for the .NET port; all other original scope and security boundaries remain.

Current .NET `GET /api/admin/dashboard` executes three global aggregate counts and returns them to every account with `admin.access`. The React dashboard renders the same three cards for all signed-in admins, though action links are capability-gated. The RBAC snapshot already combines web-guard role and direct user grants, and the API group already enforces authentication, `admin.access`, no-store responses, and CSRF for writes. F003 supplies the shared shell, heading, and responsive card styles. The F002 isolated-database test currently asserts the Super Admin global totals.

The pre-existing local working tree contains the F003 completion-document edit and this untracked F004 requirement note. These are protected work; F004 application code has not been changed during analysis. No shared application data was queried.

## Proposed implementation plan

1. Reuse the existing protected `GET /api/admin/dashboard` route. Have its handler pass the current server-side RBAC snapshot to a focused read-only dashboard method. Return a discriminated Super Admin global summary or Admin personal summary, with the global user total included for Admin only when `users.view` is granted. Do not return global role/permission catalog counts to Admin.
2. Keep count queries as database aggregates and run only those authorized for the current actor. Derive Admin primary role and effective permission count from the existing web-guard snapshot, including direct grants. Preserve the current no-store response and all F002 route/permission checks.
3. Render Super Admin's existing three global cards; render Admin's primary role and effective-permissions cards, plus a clearly labelled **Total CMS users** card only when allowed. Show View Users/View Roles only for implemented destinations and matching view permissions. Preserve the CMS overview, account actions, F003 shell, spacing, and responsive layout. Show a clear unavailable state for malformed or failed responses.
4. Update the isolated .NET dashboard tests for Super Admin global totals, Admin personal values/direct grants, permission-limited user totals, guest/unauthorized denial, and web-guard isolation. Add a small focused frontend rendering check if the dashboard card logic is extracted into a reusable component; otherwise verify it through the configured TypeScript build and user UI review.
5. Run focused checks, present the code and UI for review, then await separate approval for final validation. Do not begin F005 or change Laravel.

## Expected files and impacts

Expected application files: `backend/Authorization/RbacStore.cs`, `backend/Authentication/AuthEndpoints.cs`, `frontend/src/auth/AuthApp.tsx`, and, if needed for concise rendering/tests, a small `frontend/src/admin/` dashboard component and scoped `frontend/src/styles.css` adjustments. Update `tests/Diwakar.Cms.Auth.Tests/RbacTests.cs` and this feature document. README status may be updated only at finalization if needed.

Database: read-only aggregate queries over existing Laravel-owned users, web roles, and web permissions; no migration, seed, schema, or shared-data write. Packages: none. Security: server-side `admin.access` remains mandatory; user-count visibility requires `users.view`; Admin never receives global role/permission totals through the dashboard API; quick links remain capability-aware and destination endpoints retain their own guards. Risks: changing the dashboard response contract, stale authorization between requests, current primary-role ordering if legacy data has multiple roles, and visual regression to the F003 shell. Complexity: Medium.

## Port acceptance criteria

- [x] Guest and authenticated users without `admin.access` remain denied by the API.
- [x] Super Admin sees accurate global user, web-role, and web-permission totals.
- [x] Admin sees their primary role and effective permissions, including direct web grants, without global role/permission totals.
- [x] Admin sees the clearly labelled global CMS user total only with `users.view`.
- [x] Quick links target only implemented, authorized sections; F001 account actions remain available.
- [x] Dashboard reuses the F003 shell and responsive styling; errors and empty states are clear.
- [x] No new package, migration, schema, Laravel change, future module, or analytics feature is introduced.
- [x] Focused tests pass and the user reviews implementation before final validation.

## Implementation and focused-check record — 2026-10-29

The user approved the F004 plan. The existing `/api/admin/dashboard` route now reuses the admin endpoint filter's current server-side RBAC snapshot. Super Admin receives the same global users, web-roles, and web-permissions aggregate counts with `scope: global`. An Admin receives `scope: personal`, primary role, and an effective web-permission count that includes direct grants; only an Admin with `users.view` receives the global user count. No global role or permission catalog count is returned to Admin. The route remains authenticated and protected by `admin.access` and no-store headers.

React validates the two response shapes and renders the existing global cards for Super Admin or the approved personal cards for Admin. User/role links remain capability-aware, the F003 shell and F001 account actions stay in place, and the card grid adapts to two or three cards. Dashboard-specific request failures now identify the dashboard as unavailable. No future-module cards or links were added.

Focused checks passed: isolated .NET dashboard tests 4/4, the directly affected authenticated-unauthorized RBAC regression 1/1, frontend dashboard rendering/response tests 4/4, and the configured TypeScript/Vite production build. The first Debug test build could not replace the running local API executable; Release output compiled and passed without stopping the user's API process. Tests used only `dss_enterprise_cms_dotnet_test` and did not write to the shared application database.

Files changed for F004: `backend/Authorization/RbacStore.cs`, `backend/Authentication/AuthEndpoints.cs`, `frontend/src/auth/AuthApp.tsx`, `frontend/src/styles.css`; new `frontend/src/admin/DashboardSummary.tsx`, `frontend/tests/dashboard.test.mjs`, and `tests/Diwakar.Cms.Auth.Tests/DashboardTests.cs`; this document. The pre-existing uncommitted F003 completion-document change remains protected. No migration, package, Laravel code, or governance file changed. The running Debug API must be restarted to serve the new response contract for UI review; the agent has not stopped it. Legacy users with multiple web roles retain the F002 first-role display convention, though the CMS product model uses one primary role.

## UI review correction — stale local API process

The user reported "Dashboard data is unavailable" while the new React code was being served. The local API was still running the pre-F004 Debug executable started at 16:59; the new backend code had been compiled only to Release for focused tests. The agent stopped only that verified project API process and restarted the updated Debug API at 18:03. Direct and Vite-proxied health checks returned HTTP 200; an unauthenticated dashboard request returned the expected 401. This was a local process/version mismatch, not a database or application-code defect. The signed-in dashboard required the user's browser refresh and visual confirmation at that stage. No shared data, tracked code, package, or schema was changed for this correction.

At that stage, user code/UI review and final validation were still pending.

## Deferred navigation improvement — after F008

The user asked to defer smoother React admin navigation until all approved F001–F008 milestones are finished. Current menu links trigger full document navigation and re-fetch account, authorization, and page data. A later, separately approved task may add client-side navigation while preserving direct URLs, server-side authorization, and the F003 shell. No navigation code or package change is part of F004.


## Final validation and finalization — 2026-10-29

After reviewing the dashboard, the user indicated that F004 appeared complete and authorized the next validation step. The complete isolated .NET Release suite passed (37/37) after repairing test isolation: the test fixture now restores the baseline Admin role permissions between cases, so an earlier RBAC test cannot affect later dashboard assertions. The frontend test set passed (8/8), and the TypeScript/Vite production build passed. These checks used only the dedicated `dss_enterprise_cms_dotnet_test` database for .NET integration tests; no shared application database was reset or seeded.

Final security, validation, authorization, migration, regression, and diff reviews found the dashboard still requires authentication and `admin.access`; its server-side response limits Admin to their personal role and effective permissions, exposing a global user count only with `users.view`. Client links also follow view permissions. Existing account actions remain available, and the dashboard has a clear unavailable state. No new package, migration, schema change, Laravel change, future-module work, secret, debug code, or remote Git operation was introduced. The F003 completion-document edit predated F004 and remains separate from this feature's work.

F004 is ready for the user's final approval. It must not be marked Complete, and F005 must not begin, until that approval is given.
