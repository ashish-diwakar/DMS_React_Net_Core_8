# F002 — Roles, Permissions and Super Admin (.NET port)

Status: Complete

## Objective and authority

Add server-enforced authorization to the completed F001 authentication implementation. Reuse Laravel's existing Spatie schema and approved behavior, including guard/model isolation, protected Super Admin operations, and minimal role-management screens. Active root: C:\Ashish\Projects\DiwakarEnterpriseCMS\DMS_React_Net_Core_8.

The user approved this implementation plan and selected .NET-only locking. Laravel and governance files remain protected. Application writes exercised in tests were confined to the isolated test database. Laravel reference source is the separate local project C:\Ashish\Projects\DiwakarEnterpriseCMS\dss-enterprise-cms. Historical statements in its F002 specification are superseded by current approved source and completion records.

## Current behavior and compatibility findings

- Existing roles: Super Admin and Admin. Admin defaults are admin.access, users.view, and roles.view. Do not reset customized assignments or permissions.
- Core F002 capabilities are admin.access, users.view, users.assign_roles, roles.view, roles.manage_permissions. Current Laravel Rbac.php additionally recognizes settings.manage, media.manage, pages.manage from already completed Laravel features. Preserve these existing grants; do not implement their .NET modules or create speculative permissions.
- Laravel Gate::before grants Super Admin every checked ability. The super-admin.manage ability is reserved to that bypass; possessing an ordinary permission with that name must never grant Super Admin management.
- Admin routes require authentication and admin.access. User/role list and write actions require their corresponding capabilities independently. Ordinary Admin has read access by default, with no role/permission writes.
- Existing user-role UI replaces a user's primary role. Read existing web roles for authorization; editing assigns one web role. Preserve other guards and model types.
- Only Super Admin may assign/remove that role. The last Super Admin cannot be demoted. Even Super Admin cannot edit the protected role's permission list through ordinary role management.
- Direct user permissions are supported by the installed Spatie evaluator, although the CMS exposes no direct-grant UI. Read existing direct grants to preserve effective authorization; never add/remove them through the new UI.
- Public registration remains disabled. Full user CRUD, role/permission CRUD, invitations, tenant support, activity logging, F003 navigation, and F004 metrics are excluded.

Read-only Laravel runtime configuration inspection confirmed guard web; model type App\Models\User; teams and wildcard permissions disabled; tables roles, permissions, model_has_roles, model_has_permissions, role_has_permissions. Permission cache uses the database store, table cache, default database connection, prefix laravel-cache-, key spatie.permission.cache. No shared user records or live role assignments were queried.

## Approved concurrency boundary

The user approved keeping Laravel unchanged and protecting .NET role writes with a shared Super Admin row lock.

The plan below assumes Laravel stays unchanged. Its current last-Super-Admin protection counts users and then syncs roles without a transaction. .NET can serialize its own role changes, but cannot guarantee the invariant across simultaneous Laravel and .NET demotions unless both writers adopt the same locking protocol. Existing Laravel behavior must not be silently represented as a cross-application concurrency guarantee. Do not perform simultaneous Super Admin demotions through both applications under the unchanged-Laravel option. If coordinated protection is required, revise the plan and obtain approval before changing either implementation.

## Proposed implementation

1. Add a focused RBAC store and ASP.NET Core authorization requirements/handler. Load effective web-guard role/direct grants freshly for each request, with request-local reuse only. Reuse F001's authenticated identity; never trust roles posted by clients or stale permission claims in cookies. Centralize the Super Admin bypass and its reserved management capability.
2. Require admin.access on /api/admin/dashboard and each administrative endpoint. Add a safe current-authorization response for React, live dashboard counts, minimal user/role lists, and capability-protected assignment/permission updates. Use batched joins/lookups to avoid N+1 queries and return no password hashes, stamps, session data, or secrets.
3. Implement transactional write services with current actor/target validation, guard/model isolation, explicit field mapping, and a consistent locking order. Serialize .NET role mutations around the existing web Super Admin role record; recheck authorization and the last-Super-Admin rule within the transaction. Reject writes when required baseline records/configuration are missing; no automatic seeding or schema creation.
4. Invalidate only Laravel's configured permission-cache key when role/permission data changes. Use the existing database-backed cache and the same database transaction for pivot changes and key removal, with rollback on invalidation errors. No broad cache flush, PHP serialization implementation, PHP child process from HTTP requests, Redis, or new cache infrastructure. Verify the mapping from the inspected Laravel configuration before enabling writes. Future non-database caches or long-running PHP workers require a separate compatibility plan.
5. Add minimal React user/role management screens and permission-aware account links, preserving F001 authentication forms. Distinguish 401 sign-in redirects from 403 access-denied states; recheck server capabilities after changes. Add an explicit local-only .NET provisioning subcommand for an existing/new Super Admin with hidden password entry for new users, input validation, bcrypt, and atomic user/role/cache updates. It must never run at startup or automatically against shared users during testing.
6. Extend the existing isolated test fixture and focused tests, including the required five Spatie tables and cache table only in dss_enterprise_cms_dotnet_test. Update F001 tests to explicitly give their dashboard-authorized fixture users appropriate roles. Run relevant RBAC/security/authentication regression cases, frontend tests/build, then present the implementation for review. Final validation and completion remain separate gates.

Complexity: Large. Authorization, cross-framework cache compatibility, privileged provisioning, and concurrency checks form one cohesive port. No feature split or parallel agents are proposed.

## Proposed API and UI contract

- GET /api/auth/authorization: authenticated user's safe roles/capabilities, with no-store; auth-account operations remain usable without admin.access.
- GET /api/admin/dashboard: authentication plus admin.access; returns live total users, web roles, and web permissions for the F002 dashboard overview.
- GET /api/admin/users: admin.access plus users.view.
- PATCH /api/admin/users/{id}/role: admin.access plus users.assign_roles; extra Super Admin protections apply.
- GET /api/admin/roles: admin.access plus roles.view.
- PATCH /api/admin/roles/{id}/permissions: admin.access plus roles.manage_permissions; protected Super Admin role always denied.
- React /admin/users and /admin/roles: minimal management views, with links/actions conditional on server-reported capabilities. No F003 shell work.

IDs are validated positive unsigned identifiers and serialized safely as strings for JavaScript. Permission updates require an explicit array, reject unknown/unapproved/other-guard names, and support an explicit empty selection. Manage the existing eight approved names where corresponding web rows already exist; preserve any unrelated/unmanaged permission links rather than silently dropping them. Do not create missing module permissions. Role writes affect only the current user's App\Models\User web-role pivots; direct permissions and unrelated guards/models remain untouched.

Provisioning uses only explicit operator invocation and existing baseline roles; missing baseline configuration fails safely. Existing-user promotion preserves password/profile data. New users require name/email validation and F001-compatible password policy/hash. Passwords must not be command-line arguments, environment-variable defaults, logged values, or printed output. Tests exercise provisioning only against synthetic isolated records; no actual shared administrator is provisioned by the agent.

## Database, dependencies, and security

Application schema impact: none. No Laravel or .NET migration, independent Identity schema, startup seeder, role/permission catalogue reset, or destructive command. Approved user actions intentionally write shared role/permission pivots and remove the exact permission-cache entry; explicit provisioning can insert a user and assign Super Admin. These effects must be disclosed in implementation review. Laravel migrations remain authoritative.

Package impact: none expected. Reuse MySqlConnector, BCrypt.Net-Next, ASP.NET Core policy authorization, and the current .NET/Node test tooling. No Spatie .NET replacement, EF Core, Dapper, CLI library, or extra frontend package is needed.

All writes require F001 antiforgery protection and server-side authorization. Recheck sensitive invariants in transactions; rollback related writes on failures. Strictly isolate web/model identifiers, keep direct permissions read-only, and protect Super Admin regardless of hidden UI controls. Runtime reads must observe changed grants on the next request without requiring logout. Laravel may retain an in-flight/request-local snapshot; database cache invalidation is not a promise to revoke a request already authorized in another process.

## Expected files

Create backend/Authorization/ files for the permission catalogue, authorization requirement/handler, scoped snapshot/store, transactional services, and admin endpoints; backend/Commands/ provisioning entry/service; frontend/src/admin/ user/role screens; and focused RBAC test classes in the existing tests project.

Modify backend/Program.cs and authentication/authorization registration, the minimal dashboard endpoint, frontend AuthApp/api/return-path tests and styles, AuthFixture and dashboard-related F001 tests, non-secret RBAC cache configuration, README.md, SHARED_DATABASE.md, and this specification. Exact small-file grouping can follow existing conventions. Preserve F001's completed record, both AGENTS.md documents, Laravel code, and all unrelated user work under the recommended scope.

## Acceptance and focused test plan

- Guests receive 401 for admin APIs; authenticated unprivileged users receive 403; normal Admin can read but cannot write by default.
- Super Admin bypass works without ordinary permission rows; an api-guard or other-model lookalike never grants CMS access. Reserved Super Admin management cannot be obtained from an ordinary direct/role permission.
- Existing direct and role-derived web permissions are honored, without a direct-grant UI. Changes are observed on the next .NET request.
- Authorized users can list users/roles and assign one web role; wrong IDs/guards, CSRF failures, permission forgery, and unauthorized writes leave records unchanged.
- Only Super Admin can promote/demote Super Admin. Last-member protection and concurrent .NET demotion tests pass. Cross-application concurrency remains limited: .NET writes serialize with each other, while Laravel remains unchanged and does not share the lock protocol.
- Protected Super Admin role permissions cannot be edited. Valid updates affect only approved existing web permissions, preserve unrelated links, and support intentionally clearing managed permissions.
- Exact Laravel cache invalidation works, unrelated cache keys survive, and invalidation/write failures rollback the transaction. Verify the cache contract against the installed Laravel database-cache implementation using only an explicitly isolated connection.
- Provisioning preserves existing users' credentials, securely creates valid synthetic users when explicitly requested, is repeatable, rejects invalid/duplicate input, and has no automatic shared-database side effects.
- Existing F001 login/logout/recovery/verification behavior and frontend redirect/CSRF tests still pass. No real shared users, passwords, roles, or cache rows are changed by tests.

## Implementation record and review gate

Implementation approval received after the user approved the .NET-only concurrency boundary. Status remains In Progress pending user code review. No packages, migrations, governance changes, Laravel code changes, or shared application records were changed.

Implemented API behavior: admin.access protects dashboard and admin groups; users.view/users.assign_roles and roles.view/roles.manage_permissions gate list and mutation endpoints. Permission snapshots are queried from the current web/model pivots per request. Direct grants are honored read-only. Super Admin bypasses ordinary capabilities, while super-admin.manage remains reserved to that bypass. Responses contain safe account/role data only. Mutations require F001 antiforgery validation.

Role assignments replace only App\\Models\\User web-role pivots, preserve other guards and direct grants, require Super Admin when promoting/demoting the protected role, and reject last-member demotion. Role permission edits protect Super Admin, allow only the existing approved eight web permission names, preserve unmanaged links, and allow an explicit empty list. .NET role writes serialize on the existing Super Admin web-role row and recheck state within transactions. Only Laravel's exact configured database cache key is invalidated in the same transaction. Laravel remains unchanged; cross-application simultaneous demotions are not globally serialized and must be avoided.

Added the explicit `provision-super-admin` local command. Existing user credentials/profile are preserved; new users require validated name/email and hidden matching password entry. User/role/cache updates are transactional. The command is never run automatically. Tests call its service against synthetic isolated records only.

Added React `/admin/users` and `/admin/roles` views, permission-aware dashboard links, safe return paths, and 401/403-aware API use. No future CMS module functionality was added.

Files changed: backend/Authorization/RbacStore.cs and RbacProvisioner.cs; backend/Authentication/AuthConfiguration.cs and AuthEndpoints.cs; backend/Program.cs; frontend/src/admin/AdminPage.tsx; frontend/src/auth/AuthApp.tsx and api.ts; frontend/src/styles.css; frontend/tests/auth.test.mjs; tests/Diwakar.Cms.Auth.Tests/AuthFixture.cs, AuthenticationTests.cs, and new RbacTests.cs; README.md, docs/SHARED_DATABASE.md, and this specification. Existing user changes: the F002 specification was the only pre-existing untracked file at implementation start; all other listed edits are from this F002 implementation. No unrelated tracked changes were present.

Database impact: no application migrations/schema changes. The isolated test database fixture now creates nine minimal tables and clears only that database. Shared-table writes tested were confined to synthetic role/user/cache records there. No shared Laravel database role/user/cache records were queried or modified. Normal user/admin actions and explicit CLI invocation intentionally write the shared tables as described above.

Dependency impact: no package changes. Focused checks: seven `RbacTests` passed; twenty `AuthenticationTests` passed (27 total); frontend auth tests passed (2); frontend TypeScript/Vite production build passed. Node 24 reports its existing native TypeScript-stripping experimental warning. A prior initial RBAC run exposed a fixture SQL model-identifier escaping issue; it was corrected and the focused suite then passed.

Assumptions and known limitations: the shared schema and database cache settings match the inspected Laravel source. Super Admin concurrency protection serializes .NET writers only; Laravel's unchanged writer does not participate. Already-running PHP decisions may retain request-local state; alternate cache providers or long-running worker behavior needs a separate compatibility plan. The user list is bounded to the first 500 records and has no pagination in this minimal F002 screen. No production/shared account was used for UI or provisioning tests.

Focused testing is complete. Please review the changes. May I proceed with final validation?

User-requested UI review correction: added a shared admin shell matching the supplied Laravel reference (dark left panel, grouped navigation, white identity bar, role label/avatar, pale content area) and changed the users list to its compact role-assignment table. F002 pages share the shell; Settings, Media Library, and Pages appear as inactive labels where the existing Laravel capability exists, because those .NET modules are not in F002. Frontend auth tests passed again after this correction. A subsequent frontend build could not run because the existing frontend/node_modules/typescript folder lacks package.json and the compiler executable (tsc); restoring dependencies would require npm ci, which has not been authorized or run. No backend code changed in this correction.
The follow-up Vite parse report was caused by orphaned authentication-page JSX left after the admin-shell early return. The component structure has been repaired; Vite returned HTTP 200 when transforming AuthApp.tsx, AdminShell.tsx, and AdminPage.tsx. The standalone TypeScript production build is still unverified because the local compiler package remains incomplete.

Follow-up roles UI revision: permission choices now use bordered three-column checkbox cards, aligned spacing, and a Save Permissions action to match the supplied reference; the protected Super Admin explanation remains visible. Vite component/style transforms and frontend auth tests passed after this revision.
The Save Permissions button is placed beneath the full permissions grid and aligned to the left, per the latest visual correction. Vite transformation and frontend auth tests passed.

Dashboard UI update requested by the user: the prior account-only confirmation is now a protected overview with live total user accounts, web-guard roles, and web-guard permissions, matching the supplied three-card reference. Change password, confirm password, verification, and sign-out actions remain available as styled account actions. The dashboard summary is read-only and uses the isolated test database for its new count assertion. Seven RBAC tests and two frontend auth tests passed after the update; Vite transformed the changed modules successfully.

UI consistency correction: consolidated public, authentication, and admin styling into one token-based spacing, typography, control, card, and responsive system. Page selectors are scoped to their respective layout so headings and grids do not affect unrelated routes. The existing account and admin actions remain in place. Vite transformed the modified React modules and stylesheet, and both frontend auth tests passed. A full production build remains unavailable until the previously identified incomplete local TypeScript installation is restored.

Dashboard reference correction: the shared admin sidebar is 286 px, the header is 64 px, and the content uses a centered 1280 px maximum width, matching the supplied Laravel proportions. The dashboard overview now contains only its heading and description; the three metric cards follow it, with account actions in a separate card below. Metrics require valid nonnegative integer counts, so a stale or incomplete API response shows an error instead of blank values; account links and sign-out stay available. The identified local development API was restarted with the existing local launch profile and is healthy through the frontend proxy. Vite transformed the dashboard and stylesheet successfully. This correction remains at the user code review gate; final validation has not begun.

Profile menu review correction: the shared admin identity control now opens a right-aligned account panel showing the current name and role with a Log out action, matching the supplied screenshot. The menu closes on outside click or Escape and returns keyboard focus to its trigger on Escape. Log out reuses the existing CSRF-protected F001 endpoint, handles a stale 401 session, and displays request failures inside the panel. No backend, database, package, Laravel, or governance changes were made. Vite transformed AdminShell.tsx and the stylesheet with HTTP 200; two focused frontend auth tests passed; git diff --check found no whitespace errors. The change remains at the user review gate.

## Approved Create Admin scope addendum

The user approved adding only Admin account creation to F002 and explicitly deferred a separate ordinary User account flow. The existing full-user-CRUD exclusion still applies to edit, delete, invitations, and public registration. The Super Admin only Create Admin card sits above the Users table; normal Admin never sees it. The POST /api/admin/users endpoint independently enforces Super Admin membership rather than trusting a client flag or an ordinary permission grant.

Name, email, initial password, and confirmation are validated server-side. Email is normalized; duplicate accounts are rejected. Creation inserts a shared user with an unverified email and a PHP-compatible bcrypt password, assigns the existing web Admin role, and invalidates only the configured Laravel permission-cache key within one transaction. Missing baseline Admin role fails without an account insert. The initial password is supplied manually by Super Admin; no invitation email is sent. No schema, package, Laravel code, governance file, or separate User role is changed.

Focused implementation checks: three new isolated-database cases cover guest/Admin/forged permission rejection and CSRF, valid Admin creation/login/cache behavior plus duplicate/invalid input, and missing-role rollback. All three passed after fixing test fixture restoration; the directly affected RBAC class passed 10/10. The test checks the assigned Admin role rather than assuming a fixed permission set, because that role can be customized. The two existing frontend auth tests passed; Vite transformed AdminPage.tsx, CreateAdminForm.tsx, and styles.css with HTTP 200. The updated local development API was restarted and is healthy through the frontend proxy. Full frontend production build remains unavailable because the local TypeScript package is incomplete. The user subsequently approved final validation.

## Final validation progress — 2026-10-29

The user approved final validation. The full isolated .NET suite passed 34/34, and the frontend authentication tests passed 2/2. The route, authorization, validation, transaction, database, and diff review found no F002 migration, package-manifest change, secret, unapproved remote operation, or unrelated feature. Super Admin only account creation is checked both in React and in the transactional API; ordinary Admin requests remain forbidden. Existing Laravel code and shared application records were not changed by validation. The known cross-application concurrency limitation remains documented.

The configured frontend production build did not reach bundling: `npm run build` stopped because the checkout's ignored `frontend/node_modules` contains neither the TypeScript compiler executable nor the Vite package. A direct Vite build was also unavailable. The tracked lockfile and manifest are present. Restoring the already declared locked packages with `npm ci` requires separate package-installation approval under AGENTS.md. F002 stays In Progress until that check passes, any discovered compiler errors are resolved, and the user gives final completion approval.

## Final validation and completion review — 2026-10-29

The user approved final validation and the separate restoration of existing lockfile dependencies. `npm ci --no-audit --no-fund` restored the ignored frontend install without changing package.json or package-lock.json. The configured `npm run build` passed TypeScript checking and Vite production bundling. The local Vite server was restarted and its API proxy returned HTTP 200.

Acceptance review: authentication and admin access are enforced server-side; role and direct web grants are resolved from current shared records; Super Admin remains a protected bypass; user-role and role-permission writes validate their inputs, guards, and actor rights; the last-Super-Admin rule serializes .NET writes; the exact Laravel database permission-cache key is invalidated transactionally; explicit provisioning has no startup side effect; the Users/roles/dashboard UI and Super Admin only Create Admin form match the approved scope; F001 account actions remain available. The unchanged Laravel writer does not join the .NET lock protocol, as previously approved and documented. The Users list remains limited to 500 records. No User account creation, invitation, future CMS module, or public registration was added.

Final checks: isolated .NET tests 34 passed, 0 failed/skipped; frontend authentication tests 2 passed, 0 failed; TypeScript/Vite production build passed. Security, validation, authorization, database, migration, regression, and final diff review found no unresolved F002 code issue, schema migration, tracked dependency change, secret, debug artifact, or unrelated feature. Tests did not write to the shared application database. README and shared-database documentation reflect the approved behavior. No architecture document or ADR change was required.

The user gave post-validation final approval on 2026-10-29. F002 is Complete. The agent has not created a commit or used any Git remote.
