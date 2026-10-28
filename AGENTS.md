# React and .NET CMS instructions

This folder is a separately approved React and ASP.NET Core 8 implementation of Diwakar Enterprise CMS. The user approved this stack, this folder, F001–F008 as the overall scope, and use of the existing database. This approval does not authorize implementing all features at once.

## Governance

The parent AGENTS.md remains authoritative for approval gates, scope, security, database safety, existing work protection, packages, costs, reporting, and local Git restrictions. The approved platform exception applies only inside this folder: React and TypeScript replace Blade and Alpine; ASP.NET Core 8 replaces Laravel application code. The original Laravel project and its governance documents must remain unchanged.

Use a single agent. Work on one approved milestone or feature at a time. Do not install new packages, change architecture, implement another feature, perform final validation, or mark work Complete without the applicable explicit approval. No remote Git operations or automatic local commits or branch changes. Report AI usage as unavailable when runtime values are unavailable.

## Active milestone

Read docs/FOUNDATION.md before foundation work. The approved stage is implementation and focused checks, followed by user code review. Final validation and completion require separate approvals.

## Engineering conventions

- Target net8.0. Keep HTTP endpoints thin; use services for meaningful business logic, dependency injection, and transactions for related writes.
- Use ASP.NET Core server-side validation, authentication, and authorization when those features are approved. React route guards do not grant authorization.
- Use secure cookies and CSRF protection for cookie-authenticated writes. Do not disable controls to make development work.
- Use React components for reusable UI, TypeScript strict checking, semantic HTML, and accessible forms. Avoid unescaped HTML without approved sanitization.
- Keep secrets server-side in environment variables or .NET user secrets. Never put database credentials in Vite variables, tracked settings, or the browser bundle.
- Preserve framework security defaults. Do not add packages or architectural layers speculatively.

## Shared database and feature scope

Read docs/SHARED_DATABASE.md before database work. Laravel migrations remain the schema authority. Never create an independent conflicting migration history, bootstrap Identity tables, reset the database, run automatic migrations, seed existing data, or change password formats without approval. Database access begins only with an approved feature requiring it.

F001–F008 refer to the parent docs/features specifications and current approved behavior. Those Laravel specifications are reference requirements, not permission to copy framework-specific instructions literally or modify their completed records. Before each port, inspect only its relevant specification and implementation, write a .NET-specific feature specification inside this folder, resolve differences, and obtain implementation approval. Later approved behavior takes precedence over historical behavior; for example, do not reopen public registration merely because the original F001 allowed it temporarily.

Map the existing Spatie roles and permissions during F002 instead of substituting an unrelated permission model. Shared credentials do not imply shared sessions. Media sharing and reset-token compatibility need explicit feature analysis before implementation. Do not implement F009–F024.
