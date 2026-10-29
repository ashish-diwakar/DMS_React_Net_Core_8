# F003 — Admin Application Shell (.NET port)

Status: Implementation review

## Scope and source

Port the completed Laravel F003 admin-shell behavior to the approved React and ASP.NET Core 8 project. The Laravel feature specification at `../dss-enterprise-cms/docs/features/F003_ADMIN_SHELL.md` is the reference, with React and .NET conventions replacing Blade, Tailwind, Alpine, and Breeze details. F001 and F002 are Complete. Earlier user-approved F002 visual work already introduced the shared shell, profile menu, desktop sidebar, dashboard, Users, and Roles views; preserve those reviewed visuals and behaviors.

## Analysis findings

- `frontend/src/admin/AdminShell.tsx` already provides one shell, dark 286 px desktop sidebar, 64 px top bar, identity menu, secure API logout, capability-gated Users/Roles links, active state, and a shared content container. `AuthApp.tsx` uses it for Dashboard, Users, and Roles, with a common heading CSS class. F002 dashboard content and account actions are already present; F003 must not expand dashboard business logic.
- The current mobile drawer has a toggle and backdrop but no in-drawer close control or Escape/focus handling. CSS translation alone leaves the closed sidebar's links keyboard reachable. These are the main responsive/accessibility gaps.
- `AuthApp.tsx` currently renders the shell for any authenticated user with an authorization response, even when `canAccessAdmin` is false. Backend `/api/admin` endpoints already require authentication and `admin.access`; F003 should avoid showing the admin shell to an unauthorized user while retaining those server checks.
- Settings, Media Library, and Pages are existing inactive text labels shown only where the user's shared Laravel permissions allow, per the earlier approved screenshot and F002 review. They are not links, routes, or implemented modules. Keep them inactive; do not add future module destinations.
- The local working tree already has two F002 completion-document edits (`README.md`, `docs/F002_RBAC.md`). They are protected and outside the F003 implementation scope. No F003 application edits have been made during analysis.

## Proposed implementation

1. Keep the existing `AdminShell` component and visual dimensions. Make mobile navigation operable by keyboard: a labelled open/close control, Escape close, focus return to the trigger, and closed-drawer tab suppression. Ensure backdrop and navigation clicks close it.
2. Consolidate the existing Dashboard, Users, and Roles page heading markup into a small shared React component or equivalent shell prop so title and description spacing remain consistent without changing the F002 screens' content.
3. Gate `AuthApp` shell rendering on `canAccessAdmin`. Show an access-denied state outside the shell for authenticated users without that permission. Continue to rely on the existing .NET API authorization for every admin request.
4. Preserve permission-aware navigation, active styling, profile menu, CSRF-protected POST logout, and the already approved inactive labels. Make only scoped CSS adjustments needed for the mobile and heading behavior.
5. Run focused existing authorization/frontend checks, TypeScript and production build, and manually inspect desktop and narrow viewport navigation/account controls. Present code and UI for user review before final validation.

## Expected files

`frontend/src/admin/AdminShell.tsx`, `frontend/src/auth/AuthApp.tsx`, and `frontend/src/styles.css`; optionally one small shared heading component and a focused frontend test if the implementation yields testable navigation/access logic. This feature document records decisions and results. No backend or Laravel code change is planned.

## Impacts and risks

Database and migrations: none. Packages/dependencies: none. Security: preserve all server-side admin authorization, same-origin cookie and CSRF logout, and avoid exposing the admin shell to a user without `admin.access`. Regression risks: mobile focus behavior, existing desktop layout, capability-based links, account menu/logout, and F001 account actions. Focused checks should cover those behaviors without writing to the shared application database. Complexity: Small.

## Acceptance criteria for this port

- [ ] Dashboard, Users, and Roles use one consistent admin shell and page heading.
- [ ] Desktop sidebar and top bar retain the approved appearance and active/permission-aware navigation.
- [ ] Mobile navigation opens, closes, and remains keyboard usable with sensible focus behavior.
- [ ] Identity menu and secure logout remain usable; F001 account actions remain available.
- [ ] Users without `admin.access` do not receive the admin shell; backend protection remains intact.
- [ ] No future module link, database change, new package, or unrelated feature is introduced.
- [ ] Focused checks pass and the user reviews the implementation before final validation.

## Implementation and focused-check record — 2026-10-29

The user approved the plan. The existing shell was retained. The mobile drawer now has an in-drawer close button, Escape close and focus return, a keyboard focus loop while open, and CSS visibility that removes closed navigation from keyboard tab order. The top-bar toggle identifies its controlled sidebar. A small shared heading component now renders Dashboard, Users, and Roles headings. Authenticated users lacking `admin.access` receive an access-denied account view outside the shell, with a CSRF-protected sign-out action. The backend authorization and F002 content were not modified.

Focused frontend checks passed: the existing authentication tests 2/2; new server-rendered admin-shell tests 2/2 (identity, controls, active and permission-aware navigation); TypeScript and Vite production build passed. `git diff --check` found no whitespace errors. The focused isolated .NET RBAC suite was attempted and reported 10 connection failures before assertions because local MySQL is not listening on configured 127.0.0.1:3306; no shared application database was accessed. The same suite passed during F002 final validation before this frontend-only change. Browser-based mobile and desktop review remains for the user code/UI review gate. No package, schema, backend, Laravel, or governance change was made.

Known limitation for review: backend regression checks need the local isolated MySQL test service available before final validation. The earlier F002 completion edits in README.md and docs/F002_RBAC.md were present before F003 implementation and remain protected. No commit or remote Git operation was performed.

## User review correction — Roles read-only layout

The user requested that non-editing viewers see the same permission-card grid used by an editor. `frontend/src/admin/AdminPage.tsx` now renders all managed permissions, plus any additional granted permissions, as checked or unchecked **disabled** checkboxes in the existing grid. The read-only view has no Save Permissions button. The existing `canManagePermissions` check still controls editing, and the protected Super Admin role explanation remains unchanged. No backend authorization or database behavior changed. The focused frontend TypeScript/Vite build passed; `git diff --check` found no whitespace errors. Browser review remains pending.

The next gate is user code/UI review. Final validation and Complete status require separate approvals.
