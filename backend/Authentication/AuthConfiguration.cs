using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;

namespace Diwakar.Cms.Api.Authentication;

public static class AuthConfiguration
{
    public static void AddCmsAuthentication(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        var keys = Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, ".local", "keys"));
        var protection = builder.Services.AddDataProtection().SetApplicationName("Diwakar.Cms.ReactDotnet").PersistKeysToFileSystem(keys);
        if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();

        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<AuthThrottle>();
        builder.Services.AddSingleton<PasswordHasher>();
        builder.Services.AddSingleton(_ =>
        {
            var settings = configuration.GetSection("Database");
            var connection = new MySqlConnectionStringBuilder
            {
                Server = settings["Server"] ?? "127.0.0.1", Port = settings.GetValue<uint?>("Port") ?? 3306,
                Database = settings["Name"] ?? throw new InvalidOperationException("Server-side database configuration is required."),
                UserID = settings["User"] ?? "", Password = settings["Password"] ?? "",
                ConnectionTimeout = 5, DefaultCommandTimeout = 10,
            };
            return new MySqlDataSourceBuilder(connection.ConnectionString).Build();
        });
        builder.Services.AddScoped<SharedUserStore>();
        builder.Services.AddScoped<Diwakar.Cms.Api.Authorization.RbacStore>();
        builder.Services.AddScoped<Diwakar.Cms.Api.Authorization.RbacProvisioner>();
        builder.Services.AddIdentityCore<CmsUser>().AddUserStore<SharedUserStore>().AddDefaultTokenProviders();
        builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromMinutes(60));
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<DevelopmentMail>();

        builder.Services.AddAuthentication(AuthService.Scheme).AddCookie(AuthService.Scheme, options =>
        {
            options.Cookie.Name = "Diwakar.Cms.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.SlidingExpiration = false;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(120);
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
            options.Events.OnValidatePrincipal = async context =>
            {
                var store = context.HttpContext.RequestServices.GetRequiredService<SharedUserStore>();
                var user = await store.FindByIdAsync(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "", context.HttpContext.RequestAborted);
                if (user is null || !AuthService.StampMatches(context.Principal?.FindFirstValue(AuthService.StampClaim), user))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(AuthService.Scheme);
                }
                else context.HttpContext.Items[typeof(CmsUser)] = user;
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "Diwakar.Cms.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("auth-ip", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16384);
    }
}
