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
        await ExecuteAsync("DELETE FROM users");
        await ExecuteAsync("DELETE FROM password_reset_tokens");
        await ExecuteAsync("DELETE FROM sessions");
    }

    public async Task<CmsUser> SeedAsync(string email = "existing@example.test", string? hash = null)
    {
        hash ??= seedHash.Value;
        await ExecuteAsync("INSERT INTO users (name, email, password, remember_token, created_at, updated_at) VALUES ('Existing User', @email, @hash, 'unchanged-remember-token', UTC_TIMESTAMP(), UTC_TIMESTAMP())", ("@email", email), ("@hash", hash));
        await using var source = new MySqlDataSourceBuilder(ConnectionString).Build();
        return (await new SharedUserStore(source).FindByEmailAsync(email))!;
    }

    public Factory CreateFactory() => new(this);
    public Task DisposeAsync() => Task.CompletedTask;

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
            }));
            builder.ConfigureServices(services =>
            {
                // Replace the source explicitly: no test host can fall back to the shared database.
                if (new MySqlConnectionStringBuilder(fixture.ConnectionString).Database != TestDatabase)
                    throw new InvalidOperationException("Unsafe test host database.");
                services.RemoveAll<MySqlDataSource>();
                services.AddSingleton(_ => new MySqlDataSourceBuilder(fixture.ConnectionString).Build());
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
