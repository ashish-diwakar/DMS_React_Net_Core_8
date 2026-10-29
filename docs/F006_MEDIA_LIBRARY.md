# F006 — Media Library (.NET port)

Status: Complete

## Source and approved baseline

The user authorized F006 analysis after F005 was marked Complete. The reference is the completed Laravel feature at `../dss-enterprise-cms/docs/features/F006_MEDIA_LIBRARY.md`, including its later approved multi-file upload, Display Name, image/PDF preview, and asynchronous-save refinements. F006 in this application must use the shared Laravel database and preserve the F001–F005 auth, RBAC, shell, and Settings behavior. No application code or shared media/database data was changed during analysis; the local .NET working tree was clean before this document.

Laravel already has a `media` table with `id`, `original_name`, nullable `title`, `disk`, `path`, `mime_type`, `size`, nullable `width`/`height`, timestamps, and an index on `created_at,id`. Laravel saves files to its `public` disk under `media/YYYY/MM/{generated-name}`. The local public root exists at `C:\Ashish\Projects\DiwakarEnterpriseCMS\dss-enterprise-cms\storage\app\public`; `public/storage` is a junction to it. The `page_sections.media_id` foreign key and Laravel's delete action already protect referenced media, even though the .NET Page Sections port is later. The .NET RBAC mapping already knows `media.manage`; the Admin role does not receive it by default. The .NET shell currently shows a disabled Media Library placeholder.

A database connection alone does not share files. The .NET backend's current global request-body limit is 16 KiB, so a scoped upload exception is necessary. Vite already proxies `/api` to the .NET backend, but does not serve Laravel's `public/storage` on the React origin.

## Proposed F006 decisions

- **Shared storage:** Use the same existing Laravel public-disk files, with a server-side `Media:PublicRoot` absolute-path setting. Configure the ignored local settings file for this checkout using the verified root; document the setting in the example/README. Never persist an absolute path or URL in `media`, copy files to a second store, or silently fall back to a different folder. Fail closed when the configured root is missing or unsafe. The Laravel storage junction remains unchanged.
- **Public serving:** Add a read-only public `GET /api/media/{id}/file` endpoint that resolves a DB record with `disk='public'`, validates its relative `media/...` path against the configured root, and streams only an approved stored MIME type with `nosniff`. This gives the React origin working previews/Open asset links through the existing Vite proxy without running Laravel. Laravel continues to use its own `/storage/...` URLs for the same physical files. The stored disk/path remain canonical.
- **Permissions:** Keep the single existing `media.manage` permission for browse/upload/title/delete, plus `admin.access` and authentication. Super Admin bypass remains. Do not grant Admin `media.manage` by default. Public file reads do not grant management rights.
- **Uploads:** Accept 1–10 standard multipart files per request: JPEG, PNG, WebP up to 5 MiB each; PDF up to 10 MiB each. Reject SVG, GIF, scripts, executables, mismatched extensions/content, invalid/truncated images, overlong original names, and client-provided storage paths. Validate all files before writing any. Use a server-generated 40-character random basename and trusted `media/YYYY/MM` path. Read image dimensions from validated headers without a new image package; no resizing, thumbnails, or EXIF processing. Scope the larger body limit to the upload endpoint and retain the 16 KiB default for JSON endpoints. Overall request limits will be stated in the UI.
- **Browse/edit:** Show 24 per page, newest first, search by original or display name, and All/Images/Documents filters. Cards show a constrained original-image preview or a neutral PDF representation, metadata, Open asset, and a nullable Display Name editor. Save only `title` asynchronously, update the card without a page reload, and keep the draft on failure. No folder/tag system, picker, or Website Settings logo/favicon integration in F006.
- **Deletion:** Require explicit UI confirmation. Lock the media row and reject deletion when `page_sections` references it; retain the existing foreign key as a second protection. Remove only the validated stored public file and then its record; a missing file permits metadata cleanup. Report file/DB failures honestly and preserve retryability. Future consumers must add their own reference protection. No bulk deletion or soft deletes.
- **No new package or Laravel code:** Use existing ASP.NET Core/MySqlConnector and the Laravel-owned table. The shared application schema, Laravel source, settings feature, and governance files stay unchanged.

## Proposed implementation plan

1. Add a focused media service for parameterized list/read/title operations, multipart validation, generated storage paths, shared-file access, upload cleanup, and reference-safe deletion. Register and validate the server-side public-root configuration.
2. Add `GET/POST /api/admin/media` and `PATCH/DELETE /api/admin/media/{id}` management endpoints under the existing authenticated, CSRF-protected admin group, with `media.manage` checked server-side. Add the public file-stream endpoint separately. Apply and verify the upload-only request-size allowance.
3. Build the React Media Library page in the F003 shell: permission-gated navigation, 1–10-file upload, search/filter/pagination, aligned preview cards, inline Display Name save, safe Open asset links, deletion confirmation, and clear status/errors. Use FormData without setting a JSON Content-Type.
4. Extend only `dss_enterprise_cms_dotnet_test` with Laravel-compatible `media` and minimal `page_sections` test structures. Use a dedicated temporary media root that cannot point at the shared Laravel files. Test authorization, validation/size/MIME/dimensions, multi-file behavior, metadata and path safety, browsing, public serving, title-only updates, cleanup, reference protection, and deletion failure paths.
5. Update local setup documentation and the ignored local media-root setting without changing existing credentials. Run focused .NET/frontend checks and a production build, then present code and UI for user review. Await separate approval for final validation, final completion, and F007 analysis.

## Expected files and impacts

Likely application files: new `backend/Media/` service/endpoint/validation files; `backend/Authentication/AuthConfiguration.cs` and the existing admin route registration; new `frontend/src/admin/MediaPage.tsx`; `frontend/src/admin/AdminShell.tsx`, `frontend/src/auth/AuthApp.tsx`, `frontend/src/auth/api.ts`, and `frontend/src/styles.css`. Tests: isolated `tests/Diwakar.Cms.Auth.Tests/AuthFixture.cs`, new focused media tests, and directly affected frontend navigation/client tests. Setup: this feature record, `README.md`, `backend/appsettings.Local.example.json`, and the ignored `backend/appsettings.Local.json` local media-root value only.

Database: read/write existing Laravel `media` rows and read existing `page_sections` references; no shared migration/seed/schema changes. Storage: authorized uploads and deletes affect the same public files Laravel sees; tests affect only a separate guarded test root. Dependencies: none. Security: enforce auth/`admin.access`/`media.manage` and CSRF on all management writes; content/size/extension/dimension validation, controlled paths, safe public serving, escaped display text, and no raw filesystem errors. Regression risks: cross-app edits/deletes, local root misconfiguration, request-size override, non-atomic DB/filesystem operations, and long filenames/mobile layout. Complexity: Large.

## Port acceptance criteria

- [x] Laravel-compatible records and public files are shared without a new schema, second media store, absolute persisted path, or package.
- [x] Guest/unauthorized management is denied; Super Admin and explicitly permitted Admin can manage media; public file serving is read-only.
- [x] Valid 1–10-file JPEG/PNG/WebP/PDF uploads save safe metadata and generated files; invalid type, extension, content, dimensions, count, and size are rejected before normal batch writes.
- [x] List/search/filter/pagination and image/PDF cards match the current admin shell and remain responsive.
- [x] Display Name changes only `title`, updates inline after success, and preserves drafts/errors on failure.
- [x] Open asset streams existing and new public files through the React origin without depending on a running Laravel server.
- [x] Delete requires confirmation, blocks referenced media, coordinates file/row failures, and leaves unrelated files untouched.
- [x] Focused tests use only isolated DB/storage; user review precedes final validation and completion.

## Implementation review (2026-10-29)

Implemented in the .NET port: shared-table media operations, public file serving from a configured Laravel public root, upload-only request-size allowance, permission-gated admin routes/navigation, responsive upload and browse UI, inline Display Name editing, and reference-protected deletion. No Laravel source, shared schema, migration, or package was changed. The ignored local settings file now points to the verified Laravel storage root; its credentials were preserved.

Focused checks: `MediaTests` 6 passed using only `dss_enterprise_cms_dotnet_test` and a guarded private media root; `media-page.test.mjs` and `admin-shell.test.mjs` 5 passed; `npm run build` passed. Existing app/API process may need restart to load the updated backend and local media setting. A signed-in browser review and final validation are pending. The running local API was not interrupted for this implementation review.

Known integration limit: database and filesystem changes cannot commit atomically across both systems. File deletion occurs before row deletion so a failed database deletion leaves a retryable row; an interrupted database commit can still require manual reconciliation. The scoped Kestrel upload limit was verified on a separate live local API against the isolated test database and media root.

## User review correction (2026-10-29)

The Media Library link was absent when an already-running API returned an older authorization payload without `canManageMedia`. The React sidebar and page now derive access from existing `isSuperAdmin` and `permissions` values, preserving Super Admin visibility and explicit `media.manage` grants across a backend restart. The old API process must still be restarted before F006 media endpoints are available. Focused admin-shell tests (4) and the frontend production build pass.

## Automatic list-load correction (2026-10-29)

The red message shown immediately on entering Media Library came from its automatic `GET /api/admin/media` request. The still-running pre-F006 API returned 404 through both direct and Vite-proxy URLs. The verified stale local API process was restarted from this checkout; unsigned route checks now return 401 at both URLs, confirming the F006 route is registered. React now gives a specific restart instruction for a future 404 and clears stale load errors after a successful retry. The focused Media Library page test and production build pass. Browser refresh is needed to retry the authenticated list request.

## Final validation (2026-10-29)

- User confirmed Media Library is working after the stale pre-F006 API was replaced. Final review corrections keep Super Admin navigation visible from stable role/permission values and clear stale list-load errors after successful retry.
- Full isolated .NET regression suite: 48 passed, 0 failed before two extra F006 format/limit tests were added. Final F006-focused suite after those additions: 8 passed, 0 failed. Frontend regression suite: 12 passed, 0 failed. Final frontend production build and focused UI checks passed.
- Live Kestrel check on port 5099 used a temporary account only in `dss_enterprise_cms_dotnet_test` and a private media root. A 20 KiB invalid multipart file reached F006 validation and returned 400; a 20 KiB JSON auth request retained the 16 KiB limit and returned 413. Test media rows stayed at zero. The temporary account, API process, scripts, and empty root were removed.
- Security/validation review: management endpoints require authentication, `admin.access`, `media.manage`, and CSRF on writes; Super Admin bypass follows the existing RBAC model. Public reads use only approved disk/MIME and a strict relative path under the configured root. Uploads validate count, name, extension, content signatures/image structure, dimensions, and per-file size before writing. The approved cross-system file/DB failure window remains a documented operational risk.
- Database/migration review: no shared schema change or migration; the application uses Laravel's existing `media` and `page_sections` tables. Test schema and cleanup target only the dedicated test database. No dependency, Laravel source, governance file, or remote Git operation changed.
- Diff review: no secrets, generated files, temporary diagnostics, unrelated features, or unexplained TODOs are included. The ignored local `Media:PublicRoot` setting points at the verified Laravel public storage folder. No ADR or architecture-document update is required because the approved shared-storage design was implemented without a broader architectural change.

User final approval was given on 2026-10-29. F007 analysis has not started.

## Media Library UI review corrections (2026-10-29)

The upload and search areas now follow the Laravel Media Library layout: matching heading/help text, a separate upload panel, a separate labeled search/type panel with Apply and Reset, and cards directly below. Search applies the display-name/filename query and All/Images/Documents filter together, while Reset clears both. The backend's existing list endpoint and isolated tests cover those search/filter combinations.

Media cards now match the supplied collapsed and expanded references: a constrained image/PDF preview, display name and filename, MIME/size, dimensions and upload time, a collapsed Edit display name section, an expanded inline editor with the same hint and Save/Cancel controls, and Open asset/Delete actions at the bottom. Display-name updates remain asynchronous; Cancel restores the saved title, and Delete still requires confirmation. Focused UI tests (2) and the frontend production build passed after these changes. No package, migration, Laravel source, shared data, or unrelated feature changed. The later focused UI checks and frontend build passed, and the user approved F006 completion.


## Display-name save review correction (2026-10-29)

After a successful asynchronous Display Name save, the card's Edit display name section closes and the success message remains visible below it. A failed save keeps the section open with its draft and error. Focused Media Library UI tests (2) and the frontend production build passed. The user approved F006 completion.


## Completion approval (2026-10-29)

The user explicitly approved marking F006 Complete after the Display Name save correction. The earlier full final validation passed; the focused Media Library UI tests and frontend production build also passed after the final UI change. The user requested that new reusable cards and other components be placed under a Components folder from the next feature onward. Existing feature files will be reorganized later, when separately approved. No F007 analysis or implementation has begun.

