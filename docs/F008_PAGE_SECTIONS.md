# F008 — Page Sections (.NET port)

Status: Complete

## Approved scope and source

F008 ports the completed Laravel Page Sections feature in the parent project to this separately approved React and ASP.NET Core 8 application. The original Laravel feature specification and existing Laravel page_sections migration remain the source for schema and behavior. The shared database schema remains unchanged; authorized section operations update existing shared records. This feature manages structured content for an existing Page; public page rendering, menus, SEO, theme editing, arbitrary HTML/CSS/JavaScript, uploads inside the picker, and later features remain outside F008.

The user approved the supplied Page Sections list design with responsive action buttons. New reusable cards, forms, dialogs, picker, and types reside in frontend/src/components/sections. A later approved UI correction aligned the Hero button label and URL inputs and moved their guidance below both fields.

## Data and permissions

- Use the existing Laravel page_sections table with a restrictive Page foreign key, optional restrictive Media foreign key, explicit section fields, and Page/order index. No .NET migration, independent migration history, startup seeding, or shared-schema change is introduced.
- Supported types are Hero (hero), Text Content (text), and Image with Text (image_text). Hero requires a heading, allows optional body of at most 500 characters, optional image with required description, and paired button label/URL. Text Content requires heading and body of at most 10,000 characters and has no image or button. Image with Text requires heading, body of at most 5,000 characters, approved image, and image description. Headings are limited to 200 characters; image descriptions to 255; button labels to 80; URLs to 2,048.
- Server validation rejects angle-bracket markup and unsupported fields. Section type and owning Page cannot be changed through edit. Button URLs allow a single-slash root-relative path or valid HTTP/HTTPS URL without credentials; unsafe schemes, protocol-relative paths, controls, backslashes, whitespace, and encoded-prefix bypasses are rejected.
- Every administrative section endpoint requires authentication, admin.access, and pages.manage. The Super Admin bypass remains. Writes use the existing CSRF filter. React capability checks only control display.
- The Media Picker browses existing public JPEG/PNG/WebP records by display name or filename, newest first, 24 per page. It returns only image ID, display name, same-origin runtime URL, dimensions, and pagination. It requires pages.manage rather than media.manage. It cannot upload, edit, or delete media.
- Section create, move, visibility toggle, update, and delete are Page-scoped. Related writes use a transaction and owning-Page row lock. Order is contiguous from one; create appends, move respects boundaries, and delete normalizes order. Visibility is independent of Page Draft/Published state.
- Page deletion remains blocked while sections exist. Referenced Media deletion remains blocked. Deleting a section does not delete its Media asset.

## Implementation

- backend/Sections/SectionValidation.cs, SectionStore.cs, and SectionEndpoints.cs implement type validation, safe URLs, parameterized persistence, Page-scoped operations, the image picker, and authorized routes.
- backend/Authentication/AuthConfiguration.cs and AuthEndpoints.cs register the store and routes. backend/Program.cs raises the request body limit only for section create/update JSON, allowing the approved 10,000-character Text Content body.
- frontend/src/admin/sections/SectionIndex.tsx and SectionEditor.tsx implement Page-scoped list, create, and edit screens. frontend/src/components/sections contains reusable SectionCard, SectionForm, ImagePicker, SectionDeleteDialog, TypeChangeDialog, and shared types.
- frontend/src/auth/AuthApp.tsx and api.ts add protected section paths and safe login return paths. Page list/edit actions link to Manage Sections. frontend/src/styles.css supplies the responsive list, cards, picker, and aligned form layout.
- The isolated dss_enterprise_cms_dotnet_test fixture gains only the section columns and index required by focused tests. It does not alter the shared application database. SectionTests.cs and frontend/tests/sections.test.mjs cover permissions, CSRF, types, URL safety, ownership, ordering, visibility, reference protection, picker fields, forms, cards, and paths.

No package, lockfile, Laravel source, governance file, production credential, or shared migration was changed.

## Final validation — 2026-10-30

The user approved final validation after implementation and visual review of the Hero button-field correction.

- Full isolated .NET regression suite: 67 passed, 0 failed.
- Full frontend regression set: 22 passed, 0 failed.
- Frontend production build and TypeScript typecheck: passed.
- A final picker-response review removed unused Media metadata from the F008 endpoint. The affected 11 .NET Section tests, 3 frontend Section tests, and frontend production build passed afterward.
- The live Edit Section form was inspected in Chrome after the button-field correction; its two inputs align and the guidance appears below. The local API was rebuilt and restarted; health returned 200 and an anonymous section request returned 401. Mobile behavior is governed by the narrow-width CSS rule and remains available for user review.
- Final security review confirms server authorization, CSRF on writes, Page ownership checks, type-specific validation, URL restrictions, parameterized SQL, safe React text rendering, Media reference checks, and restrictive foreign keys.
- Final migration review confirms no shared schema change. The test fixture changes only the specifically named isolated database. No new dependency or architecture decision requires an ADR.
- Final diff review found no unrelated application change, temporary debug code, secrets, generated build artifact, unapproved route, or unexplained TODO in the F008 change set. The existing frontend navigation still performs full-page loads, as previously deferred by the user.

## Acceptance criteria

- [x] Existing Laravel Pages and page_sections schema are reused without a shared migration.
- [x] Only authenticated admin.access plus pages.manage accounts can manage sections; writes require CSRF.
- [x] Hero, Text Content, and Image with Text use the approved fields and server-side limits.
- [x] Unsafe URL and unsupported field inputs are rejected; Page and section type are immutable.
- [x] Sections list, create, edit, reorder, enable/disable, and delete within their owning Page.
- [x] Order remains contiguous and boundary moves are safe.
- [x] Single-image picker searches and paginates only approved public images without Media management permission.
- [x] Referenced Page and Media deletions are blocked; section deletion retains Media.
- [x] React list/cards/forms follow the approved responsive design and place reusable UI in Components.
- [x] Full .NET/frontend regression checks, production build, security review, database review, and diff review pass.
- [x] User final approval to mark F008 Complete.

## Completion approval — 2026-10-30

The user explicitly approved marking F008 Complete. This status update changes only the F008 feature record and README. Application code, shared data, schema, dependencies, and tests were not changed or rerun for completion. F009 Menu Management is next in the Laravel roadmap and is outside the currently approved F001–F008 .NET scope; it requires separate permission before analysis.
