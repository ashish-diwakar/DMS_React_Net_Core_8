using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using Diwakar.Cms.Api.Authentication;

namespace Diwakar.Cms.Auth.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class AuthFixture : IAsyncLifetime
{
    public const string TestDatabase = "dss_enterprise_cms_dotnet_test";
    public string Root { get; } = FindRoot();
    public string ConnectionString { get; private set; } = "";
    public string MailDirectory => Path.Combine(Root, "backend", ".local", "test-mail");
    public string MediaDirectory { get; } = Path.Combine(FindRoot(), "backend", ".local", "test-media-" + Guid.NewGuid().ToString("N"));
    private readonly Lazy<string> seedHash = new(() => new PasswordHasher().Hash("existing-password"));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "backend", "Diwakar.Cms.Api.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot locate the .NET project root.");
    }

    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder().SetBasePath(Path.Combine(Root, "backend")).AddJsonFile("appsettings.Local.json").AddEnvironmentVariables().Build();
        var originalName = config["Database:Name"];
        if (originalName == TestDatabase) throw new InvalidOperationException("Application database must not be the test database.");
        var builder = new MySqlConnectionStringBuilder
        {
            Server = config["Database:Server"] ?? "127.0.0.1", Port = config.GetValue<uint?>("Database:Port") ?? 3306,
            UserID = config["Database:User"] ?? "", Password = config["Database:Password"] ?? "", ConnectionTimeout = 5,
        };
        await using (var server = new MySqlConnection(builder.ConnectionString))
        {
            await server.OpenAsync();
            await using var create = server.CreateCommand();
            create.CommandText = "CREATE DATABASE IF NOT EXISTS `dss_enterprise_cms_dotnet_test` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
            await create.ExecuteNonQueryAsync();
        }
        builder.Database = TestDatabase;
        ConnectionString = builder.ConnectionString;
        await ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS users (
                id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, name VARCHAR(255) NOT NULL, email VARCHAR(255) NOT NULL UNIQUE,
                password VARCHAR(255) NOT NULL, email_verified_at TIMESTAMP NULL, remember_token VARCHAR(100) NULL,
                created_at TIMESTAMP NULL, updated_at TIMESTAMP NULL)
            """);
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS password_reset_tokens (email VARCHAR(255) PRIMARY KEY, token VARCHAR(255) NOT NULL, created_at TIMESTAMP NULL)");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS sessions (id VARCHAR(255) PRIMARY KEY, payload LONGTEXT NOT NULL)");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS roles (id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,name VARCHAR(255) NOT NULL,guard_name VARCHAR(255) NOT NULL,created_at TIMESTAMP NULL,updated_at TIMESTAMP NULL,UNIQUE KEY roles_name_guard_unique(name,guard_name))");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS permissions (id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,name VARCHAR(255) NOT NULL,guard_name VARCHAR(255) NOT NULL,created_at TIMESTAMP NULL,updated_at TIMESTAMP NULL,UNIQUE KEY permissions_name_guard_unique(name,guard_name))");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS model_has_roles (role_id BIGINT UNSIGNED NOT NULL,model_type VARCHAR(255) NOT NULL,model_id BIGINT UNSIGNED NOT NULL,PRIMARY KEY(role_id,model_id,model_type),KEY model_has_roles_model_id_model_type_index(model_id,model_type))");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS model_has_permissions (permission_id BIGINT UNSIGNED NOT NULL,model_type VARCHAR(255) NOT NULL,model_id BIGINT UNSIGNED NOT NULL,PRIMARY KEY(permission_id,model_id,model_type),KEY model_has_permissions_model_id_model_type_index(model_id,model_type))");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS role_has_permissions (permission_id BIGINT UNSIGNED NOT NULL,role_id BIGINT UNSIGNED NOT NULL,PRIMARY KEY(permission_id,role_id))");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS cache (`key` VARCHAR(255) PRIMARY KEY,value MEDIUMTEXT NOT NULL,expiration INT NOT NULL,KEY cache_expiration_index(expiration))");
        await ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS website_settings (
                id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, site_name VARCHAR(120) NOT NULL,
                tagline VARCHAR(180) NULL, public_email VARCHAR(255) NULL,
                primary_phone VARCHAR(50) NULL, secondary_phone VARCHAR(50) NULL, address TEXT NULL,
                facebook_url VARCHAR(2048) NULL, instagram_url VARCHAR(2048) NULL,
                linkedin_url VARCHAR(2048) NULL, youtube_url VARCHAR(2048) NULL,
                x_twitter_url VARCHAR(2048) NULL, footer_text VARCHAR(500) NULL,
                created_at TIMESTAMP NULL, updated_at TIMESTAMP NULL)
            """);
        Directory.CreateDirectory(MediaDirectory);
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS media (id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, original_name VARCHAR(255) NOT NULL, title VARCHAR(255) NULL, disk VARCHAR(50) NOT NULL, path VARCHAR(255) NOT NULL, mime_type VARCHAR(100) NOT NULL, size BIGINT UNSIGNED NOT NULL, width INT UNSIGNED NULL, height INT UNSIGNED NULL, created_at TIMESTAMP NULL, updated_at TIMESTAMP NULL, INDEX(created_at,id))");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS pages (id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, title VARCHAR(200) NOT NULL, slug VARCHAR(150) NOT NULL UNIQUE, status VARCHAR(20) NOT NULL DEFAULT 'draft', created_at TIMESTAMP NULL, updated_at TIMESTAMP NULL)");
        await ExecuteAsync("CREATE TABLE IF NOT EXISTS page_sections (id BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY, media_id BIGINT UNSIGNED NULL, page_id BIGINT UNSIGNED NULL, CONSTRAINT test_page_sections_media_fk FOREIGN KEY(media_id) REFERENCES media(id) ON DELETE RESTRICT, CONSTRAINT test_page_sections_page_fk FOREIGN KEY(page_id) REFERENCES pages(id) ON DELETE RESTRICT)");
        var pageColumn = await ScalarAsync("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='page_sections' AND column_name='page_id'");
        if (Convert.ToInt64(pageColumn) == 0) await ExecuteAsync("ALTER TABLE page_sections ADD COLUMN page_id BIGINT UNSIGNED NULL");
        var pageForeignKey = await ScalarAsync("SELECT COUNT(*) FROM information_schema.table_constraints WHERE table_schema=DATABASE() AND table_name='page_sections' AND constraint_name='test_page_sections_page_fk'");
        if (Convert.ToInt64(pageForeignKey) == 0) await ExecuteAsync("ALTER TABLE page_sections ADD CONSTRAINT test_page_sections_page_fk FOREIGN KEY(page_id) REFERENCES pages(id) ON DELETE RESTRICT");
        foreach(var name in Diwakar.Cms.Api.Authorization.RbacStore.ManagedPermissions) await ExecuteAsync("INSERT IGNORE INTO permissions(name,guard_name) VALUES(@name,'web')",("@name",name));
        await ExecuteAsync("INSERT IGNORE INTO roles(name,guard_name) VALUES('Admin','web'),('Super Admin','web')");
        await ExecuteAsync("INSERT IGNORE INTO role_has_permissions(permission_id,role_id) SELECT p.id,r.id FROM permissions p JOIN roles r ON r.name='Admin' AND r.guard_name='web' WHERE p.guard_name='web' AND p.name IN ('admin.access','users.view','roles.view')");
    }

    public async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        var settings = new MySqlConnectionStringBuilder(ConnectionString);
        if (settings.Database != TestDatabase) throw new InvalidOperationException("Unsafe test database.");
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAsync()
    {
        await ExecuteAsync("DELETE FROM model_has_roles");
        await ExecuteAsync("DELETE FROM model_has_permissions");
        await ExecuteAsync("DELETE FROM cache");
        await ExecuteAsync("DELETE FROM users");
        await ExecuteAsync("DELETE FROM password_reset_tokens");
        await ExecuteAsync("DELETE FROM sessions");
        await ExecuteAsync("DELETE FROM website_settings");
        await ExecuteAsync("DELETE FROM page_sections");
        await ExecuteAsync("DELETE FROM media");
        await ExecuteAsync("DELETE FROM pages");
        await ExecuteAsync("DELETE rp FROM role_has_permissions rp JOIN roles r ON r.id=rp.role_id WHERE r.name='Admin' AND r.guard_name='web'");
        await ExecuteAsync("INSERT IGNORE INTO role_has_permissions(permission_id,role_id) SELECT p.id,r.id FROM permissions p JOIN roles r ON r.name='Admin' AND r.guard_name='web' WHERE p.guard_name='web' AND p.name IN ('admin.access','users.view','roles.view')");
    }

    public async Task<CmsUser> SeedAsync(string email = "existing@example.test", string? hash = null)
    {
        hash ??= seedHash.Value;
        await ExecuteAsync("INSERT INTO users (name, email, password, remember_token, created_at, updated_at) VALUES ('Existing User', @email, @hash, 'unchanged-remember-token', UTC_TIMESTAMP(), UTC_TIMESTAMP())", ("@email", email), ("@hash", hash));
        await using var source = new MySqlDataSourceBuilder(ConnectionString).Build();
        return (await new SharedUserStore(source).FindByEmailAsync(email))!;
    }

    public async Task<object?> ScalarAsync(string sql,params (string Name,object Value)[] parameters){var settings=new MySqlConnectionStringBuilder(ConnectionString);if(settings.Database!=TestDatabase)throw new InvalidOperationException("Unsafe test database.");await using var c=new MySqlConnection(ConnectionString);await c.OpenAsync();await using var q=c.CreateCommand();q.CommandText=sql;foreach(var (name,value) in parameters)q.Parameters.AddWithValue(name,value);return await q.ExecuteScalarAsync();}

    public Task GrantRoleAsync(CmsUser user,string role) => ExecuteAsync("INSERT INTO model_has_roles(role_id,model_type,model_id) SELECT id,@model,@user FROM roles WHERE name=@role AND guard_name='web'",("@model","App\\Models\\User"),("@user",user.Id),("@role",role));
    public Factory CreateFactory() => new(this);
    public Task DisposeAsync()
    {
        var allowed = Path.Combine(Root, "backend", ".local");
        var target = Path.GetFullPath(MediaDirectory);
        if (!target.StartsWith(Path.GetFullPath(allowed) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(target).StartsWith("test-media-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe media test cleanup path.");
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        return Task.CompletedTask;
    }

    public sealed class Factory(AuthFixture fixture) : WebApplicationFactory<Program>
    {
        public TestClock Clock { get; } = new();
        public TimeSpan TokenLife { get; set; } = TimeSpan.FromMinutes(60);
        public string EnvironmentName { get; set; } = "Development";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(Path.Combine(fixture.Root, "backend")).UseEnvironment(EnvironmentName);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Name"] = TestDatabase,
                ["Authentication:MailDirectory"] = fixture.MailDirectory,
                ["Media:PublicRoot"] = fixture.MediaDirectory,
            }));
            builder.ConfigureServices(services =>
            {
                // Replace the source explicitly: no test host can fall back to the shared database.
                if (new MySqlConnectionStringBuilder(fixture.ConnectionString).Database != TestDatabase)
                    throw new InvalidOperationException("Unsafe test host database.");
                services.RemoveAll<MySqlDataSource>();
                services.AddSingleton(_ => new MySqlDataSourceBuilder(fixture.ConnectionString).Build());
                services.RemoveAll<Diwakar.Cms.Api.Media.MediaStore>();
                services.AddScoped(provider => new Diwakar.Cms.Api.Media.MediaStore(
                    provider.GetRequiredService<MySqlDataSource>(),
                    new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    { ["Media:PublicRoot"] = fixture.MediaDirectory }).Build()));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TokenLife);
            });
        }

        public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true,
            BaseAddress = new Uri(EnvironmentName == "Production" ? "https://localhost" : "http://localhost") });

        public static async Task CsrfAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/auth/csrf");
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", body!["token"]);
        }

        public static async Task LoginAsync(HttpClient client, string email = "existing@example.test", string password = "existing-password", bool remember = false)
        {
            await CsrfAsync(client);
            var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password, remember });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await CsrfAsync(client);
        }
    }
}

[CollectionDefinition("Authentication", DisableParallelization = true)]
public sealed class AuthenticationCollection : ICollectionFixture<AuthFixture> { }
