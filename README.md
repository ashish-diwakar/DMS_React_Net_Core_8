# Diwakar Enterprise CMS — React and .NET

A separate React/TypeScript frontend and ASP.NET Core 8 backend. The foundation is Complete. F001 authentication is implemented and awaiting review. F002–F008 are not implemented yet.

## Requirements

- .NET 8 SDK; global.json selects an installed .NET 8 SDK and permits newer .NET 8 feature bands.
- Node.js and npm compatible with the locked frontend dependencies. Use the existing local Node 24 installation.
- The existing local MariaDB service must be running for authentication. The public health endpoint does not check database connectivity.

## Private local configuration

The current workspace has an ignored backend/appsettings.Local.json configured from Laravel's existing database settings without printing credentials. For a new checkout, copy backend/appsettings.Local.example.json to backend/appsettings.Local.json only if the local file does not already exist, then privately fill in the existing database credentials. Never overwrite another developer's settings or commit credentials. Environment variables Database__Server, Database__Port, Database__Name, Database__User, and Database__Password override this file.

The API maps existing users and never initializes, migrates, or seeds the application database. The local settings file and all backend/.local artifacts are excluded from Git and backend build/publish content.

Run commands from this folder. Use two terminals:

```powershell
# Terminal 1 — backend at http://127.0.0.1:5088
dotnet run --project backend/Diwakar.Cms.Api.csproj --launch-profile local
```

```powershell
# Terminal 2 — frontend at http://127.0.0.1:5178
Set-Location frontend
npm ci
npm run dev
```

Open http://127.0.0.1:5178. On page load, the frontend requests /api/health through Vite's local proxy. The backend health endpoint returns Healthy when the process is available; it does not check database connectivity. If the backend is unavailable when the page loads, the frontend displays Service unavailable after the request fails or times out. Reload the page to check again; this foundation does not continuously monitor the service.

## Focused checks

```powershell
dotnet build backend/Diwakar.Cms.Api.csproj
Set-Location frontend
npm run build
```

The frontend build includes strict TypeScript checking. Run npm run typecheck independently when only type checking is needed. While both development servers are running, GET http://127.0.0.1:5088/api/health and GET http://127.0.0.1:5178/api/health should both return HTTP 200 with Healthy.

The production frontend output is frontend/dist. Vite preview serves only the built frontend; its configuration does not proxy API requests. Production frontend/API routing, certificates, hosting, and deployment require a later approved plan. The development proxy is not a production hosting solution.

## Database and secrets

The backend uses the same MariaDB database as Laravel through MySqlConnector and PHP-compatible bcrypt through BCrypt.Net-Next. Laravel retains schema ownership. Follow docs/SHARED_DATABASE.md. Credentials belong in server-side environment variables or the ignored local settings, never frontend configuration. Data Protection keys reside under backend/.local/keys and use DPAPI encryption on Windows; production key storage needs its own approved deployment plan.

## Authentication

Open /login and use an existing Laravel account. Login opens an authentication-only /admin/dashboard confirmation without CMS data or administrative operations. Public registration remains unavailable. Account links provide password confirmation/change and optional email verification. Logout returns to the public home page.

In Development, reset and verification messages become private .eml files under backend/.local/mail. Open the local file for the link; files are not web-accessible. They contain sensitive temporary links: never commit/share them or paste them into logs. Links are .NET-specific, expire after 60 minutes, and bind to current account/password state. No real email is sent. Outside Development, recovery/resend fails closed until a mail provider is separately approved and configured.

Ordinary authentication uses a browser-session cookie and a 120-minute ticket. Remember me persists a cookie for the existing Laravel duration of 400 days. Sessions are separate; a shared password change invalidates stale .NET cookies. Password changes also rotate Laravel's remember_token; existing Laravel sessions and independently managed reset links follow Laravel's own behavior. See docs/SHARED_DATABASE.md.

## Focused authentication tests

```powershell
# From react-dotnet-cms
dotnet test tests/Diwakar.Cms.Auth.Tests/Diwakar.Cms.Auth.Tests.csproj
Set-Location frontend
npm run test:auth
```

The .NET harness creates only dss_enterprise_cms_dotnet_test and three required test tables, then clears records only within that guarded database. It explicitly replaces the test host's database source to prevent falling back to the shared database. The local account needs permission to create/use this isolated database. Tests never seed or reset dss_enterprise_cms. Test rows and private artifacts may remain in the isolated database and backend/.local/test-mail.

PHP fixtures use C:/xampp/php/php.exe by default; CMS_TEST_PHP can select another PHP executable. Synthetic inputs use stdin, not command-line credentials. Frontend tests use Node's built-in runner and native TypeScript stripping; Node 24.1 emits an experimental-feature warning. No frontend test package was added.

## Workflow

Read AGENTS.md and the active docs/F001_AUTHENTICATION.md. Implementation review precedes final validation; completion requires explicit approval. Each feature has its own approval gates. The original Laravel project remains independently runnable.
