# Diwakar Enterprise CMS — React and .NET

A separate React/TypeScript frontend and ASP.NET Core 8 backend. The foundation and F001–F004 are Complete. F005 Website Settings is ready for final approval; F006–F008 are not implemented yet.

## Requirements

- .NET 8 SDK (`global.json` selects an installed .NET 8 SDK).
- Node.js 24 and npm, compatible with the locked frontend packages.
- The existing MariaDB/MySQL database. On this Windows workstation it is installed through XAMPP at `C:\xampp`; the server listens on `127.0.0.1:3306`.
- An existing CMS login with `admin.access`. The application does not create or seed users when it starts.

## Run locally (Windows PowerShell)

Run the following steps in order. Laravel does **not** need to be running; the React and .NET applications use its existing database directly.

1. **Start the database.** Either Open XAMPP & start "Apache & MySQL" or Open `C:\xampp\xampp-control.exe` and click **Start** beside **MySQL**. Alternatively, run `& 'C:\xampp\mysql_start.bat'` in a separate terminal and keep that terminal open. Use one startup method, not both. Confirm the server port before continuing:

   ```powershell
   Test-NetConnection 127.0.0.1 -Port 3306
   ```

   Continue when `TcpTestSucceeded` is `True`. The XAMPP server's `[mysqld]` port is 3306; its `[client]` default is 3307, so a manual `mysql.exe` connection should explicitly select port 3306.

2. **Check the private backend configuration.** From the project root, create the ignored local settings file only if it is absent:

   ```powershell
   Set-Location C:\Ashish\Projects\DiwakarEnterpriseCMS\DMS_React_Net_Core_8
   if (-not (Test-Path backend/appsettings.Local.json)) {
       Copy-Item backend/appsettings.Local.example.json backend/appsettings.Local.json
   }
   ```

   Open `backend/appsettings.Local.json` privately and set `Database:Server`, `Port`, `Name`, `User`, and `Password` for the existing database. This checkout already has a local file; do not overwrite it. Environment variables `Database__Server`, `Database__Port`, `Database__Name`, `Database__User`, and `Database__Password` can override the file. Never commit credentials or put them in Vite variables. The API uses the existing schema and does not migrate or seed it.

3. **Start the .NET API** in a new terminal, from the project root:

   ```powershell
   Set-Location C:\Ashish\Projects\DiwakarEnterpriseCMS\DMS_React_Net_Core_8
   dotnet run --project backend/Diwakar.Cms.Api.csproj --launch-profile local
   ```

   Keep it running at `http://127.0.0.1:5088`.

4. **Install and start the React frontend** in another terminal:

   ```powershell
   Set-Location C:\Ashish\Projects\DiwakarEnterpriseCMS\DMS_React_Net_Core_8\frontend
   npm ci
   npm run dev
   ```

   Run `npm ci` inside `frontend`, which contains `package-lock.json`; running it at the project root produces npm `EUSAGE`. You need `npm ci` on first setup or when the lockfile changes, not on every start. Keep Vite running at `http://127.0.0.1:5178`.

5. Open `http://127.0.0.1:5178/login` and sign in with an existing authorized CMS account. The frontend proxies `/api` to the .NET API; .NET reads the shared database directly. The protected admin dashboard is at `/admin/dashboard`.

To stop the app, press Ctrl+C in the frontend and API terminals. Stop MySQL through XAMPP Control Panel, or stop its dedicated batch-file terminal if you used that method.

## Local troubleshooting

- `Request unavailable (MySqlException)` on login: check `Test-NetConnection 127.0.0.1 -Port 3306`, then verify the private database host, port, database name, and credentials. The API can start even when MySQL is down; `/api/health` only checks the API process.
- Frontend cannot reach the API: confirm the .NET terminal says it is listening on `http://127.0.0.1:5088`, then open `http://127.0.0.1:5178/api/health`. A healthy API returns HTTP 200.
- `npm ci` reports no lockfile: change to the `frontend` directory before running it. Do not generate a separate root lockfile.
- Port already in use: stop the earlier local API, Vite, or MySQL process using that port before starting another instance. The configured development ports are 5088, 5178, and 3306.

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

Open /login and use an existing Laravel account. Login opens the protected /admin/dashboard overview for users with admin.access. The overview shows live user, web-role, and web-permission counts. F002 adds capability-gated user and role screens at /admin/users and /admin/roles, backed by the existing Laravel Spatie roles/permissions tables. On the Users page, only Super Admin can see Create Admin; it creates an account with the existing Admin role and its current permissions using a manually supplied initial password. No invitation email is sent. Public registration and a separate ordinary User creation flow remain unavailable. Account links provide password confirmation/change and optional email verification. Logout returns to the public home page.

In Development, reset and verification messages become private .eml files under backend/.local/mail. Open the local file for the link; files are not web-accessible. They contain sensitive temporary links: never commit/share them or paste them into logs. Links are .NET-specific, expire after 60 minutes, and bind to current account/password state. No real email is sent. Outside Development, recovery/resend fails closed until a mail provider is separately approved and configured.

Ordinary authentication uses a browser-session cookie and a 120-minute ticket. Remember me persists a cookie for the existing Laravel duration of 400 days. Sessions are separate; a shared password change invalidates stale .NET cookies. Password changes also rotate Laravel's remember_token; existing Laravel sessions and independently managed reset links follow Laravel's own behavior. See docs/SHARED_DATABASE.md.

## Focused authentication tests

```powershell
# From DMS_React_Net_Core_8
dotnet test tests/Diwakar.Cms.Auth.Tests/Diwakar.Cms.Auth.Tests.csproj
Set-Location frontend
npm run test:auth
```

The .NET harness creates only dss_enterprise_cms_dotnet_test and nine required test tables, then clears records only within that guarded database. It explicitly replaces the test host's database source to prevent falling back to the shared database. The local account needs permission to create/use this isolated database. Tests never seed or reset dss_enterprise_cms. Test rows and private artifacts may remain in the isolated database and backend/.local/test-mail.

PHP fixtures use C:/xampp/php/php.exe by default; CMS_TEST_PHP can select another PHP executable. Synthetic inputs use stdin, not command-line credentials. Frontend tests use Node's built-in runner and native TypeScript stripping; Node 24.1 emits an experimental-feature warning. No frontend test package was added.

## Workflow

Read AGENTS.md and the active feature document in docs/. Implementation review precedes final validation; completion requires explicit approval. Each feature has its own approval gates. The original Laravel project remains independently runnable.


## F002 role management

Administrative APIs enforce admin.access and the matching users/roles capability. Super Admin permissions are protected; .NET role writes serialize through the existing web Super Admin role row and invalidate only Laravel's configured database permission-cache key in the same transaction. Provisioning is an explicit operator command: dotnet run --project backend/Diwakar.Cms.Api.csproj -- provision-super-admin [email]. Run it only when intentionally changing shared users; a new user prompts for a name and hidden password. No provisioning runs during application startup.

Laravel remains unchanged. Its role-assignment path does not use the .NET row lock, so concurrent last-Super-Admin demotions across the two applications are not globally serialized. Avoid making such demotions simultaneously. Long-running Laravel worker snapshots and alternate cache stores require a separate compatibility review.
