using System.Globalization;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace Diwakar.Cms.Api.Authentication;

public static class AuthEndpoints
{
    public static void MapCmsAuthentication(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting("auth-ip")
            .AddEndpointFilter(async (invocation, next) =>
            {
                var context = invocation.HttpContext;
                context.Response.Headers.CacheControl = "no-cache, no-store";
                context.Response.Headers.Pragma = "no-cache";
                if (!HttpMethods.IsGet(context.Request.Method))
                {
                    try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
                    catch (AntiforgeryValidationException) { return Error("The request could not be verified. Refresh the page and try again.", 400); }
                }
                return await next(invocation);
            });

        group.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));

        group.MapGet("/me", (HttpContext context, TimeProvider clock) =>
        {
            var user = Current(context);
            var confirmed = long.TryParse(context.User.FindFirstValue(AuthService.ConfirmedClaim), out var timestamp)
                && timestamp <= clock.GetUtcNow().ToUnixTimeSeconds()
                && timestamp > clock.GetUtcNow().AddHours(-3).ToUnixTimeSeconds();
            return Results.Ok(new { id = user.Id.ToString(CultureInfo.InvariantCulture), user.Name, user.Email, user.EmailVerified, passwordConfirmed = confirmed });
        }).RequireAuthorization();

        group.MapPost("/login", async (LoginInput input, HttpContext context, AuthService service, AuthThrottle throttle, CancellationToken ct) =>
        {
            if (!AuthValidation.Email(input.Email) || string.IsNullOrEmpty(input.Password)) return Error("Enter a valid email and password.", 400);
            var key = AuthThrottle.Key("login", input.Email!.ToLowerInvariant() + "|" + context.Connection.RemoteIpAddress);
            if (!throttle.Take(key, 5)) return Error("Too many attempts. Try again in a minute.", 429);
            var user = await service.FindAsync(input.Email, ct);
            if (!service.Verify(input.Password, user))
            {
                return Error("These credentials do not match our records.", 401);
            }
            throttle.Clear(key);
            await service.SignInAsync(context, user!, input.Remember);
            return Results.Ok(new { message = "Signed in." });
        });

        group.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(AuthService.Scheme);
            return Results.Ok(new { message = "Signed out." });
        }).RequireAuthorization();

        group.MapPost("/forgot-password", async (EmailInput input, IWebHostEnvironment environment, AuthService service, AuthThrottle throttle, CancellationToken ct) =>
        {
            if (!AuthValidation.Email(input.Email)) return Error("Enter a valid email.", 400);
            // Never claim delivery in an environment without an approved sender.
            if (!environment.IsDevelopment()) return Error("Email delivery is not configured.", 503);
            var started = Stopwatch.GetTimestamp();
            var key = AuthThrottle.Key("reset", input.Email!.ToLowerInvariant());
            if (throttle.Take(key, 1))
            {
                var user = await service.FindAsync(input.Email, ct);
                if (user is not null) await service.SendResetAsync(user, ct);
            }
            var remaining = TimeSpan.FromMilliseconds(200) - Stopwatch.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, ct);
            return Results.Ok(new { message = "If the account exists, a password reset link has been sent." });
        });

        group.MapPost("/reset-password", async (ResetInput input, AuthService service, CancellationToken ct) =>
        {
            if (!AuthValidation.Email(input.Email) || string.IsNullOrEmpty(input.Token)
                || !AuthValidation.NewPassword(input.Password, input.PasswordConfirmation))
                return Error("Provide a valid email, reset link, and matching password of at least eight characters.", 400);
            var user = await service.FindAsync(input.Email!, ct);
            if (user is null || !await service.ResetAsync(user, input.Token, input.Password!, ct))
                return Error("This reset link is invalid or expired.", 400);
            return Results.Ok(new { message = "Password reset. Sign in with your new password." });
        });

        group.MapPost("/confirm-password", async (PasswordInput input, HttpContext context, AuthService service, TimeProvider clock) =>
        {
            if (string.IsNullOrEmpty(input.Password) || !service.Verify(input.Password, Current(context)))
                return Error("The password is incorrect.", 400);
            var ticket = await context.AuthenticateAsync(AuthService.Scheme);
            var claims = context.User.Claims.Where(claim => claim.Type != AuthService.ConfirmedClaim).ToList();
            claims.Add(new Claim(AuthService.ConfirmedClaim, clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
            await context.SignInAsync(AuthService.Scheme, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthService.Scheme)), ticket.Properties);
            return Results.Ok(new { message = "Password confirmed." });
        }).RequireAuthorization();

        group.MapPut("/password", async (ChangeInput input, HttpContext context, AuthService service, CancellationToken ct) =>
        {
            var user = Current(context);
            if (string.IsNullOrEmpty(input.CurrentPassword) || !service.Verify(input.CurrentPassword, user)
                || !AuthValidation.NewPassword(input.Password, input.PasswordConfirmation))
                return Error("Check your current password and provide a matching new password of at least eight characters.", 400);
            if (!await service.ChangeAsync(user, input.Password!, ct)) return Error("Your account changed. Sign in again.", 409);
            await context.SignOutAsync(AuthService.Scheme);
            return Results.Ok(new { message = "Password changed. Sign in with your new password." });
        }).RequireAuthorization();

        group.MapPost("/verification-notification", async (HttpContext context, IWebHostEnvironment environment, AuthService service, AuthThrottle throttle, CancellationToken ct) =>
        {
            var user = Current(context);
            if (user.EmailVerified) return Results.Ok(new { message = "Email already verified." });
            if (!environment.IsDevelopment()) return Error("Email delivery is not configured.", 503);
            if (!throttle.Take(AuthThrottle.Key("verify", user.Id.ToString(CultureInfo.InvariantCulture)), 6))
                return Error("Too many requests. Try again in a minute.", 429);
            await service.SendVerificationAsync(user, ct);
            return Results.Ok(new { message = "Verification link sent." });
        }).RequireAuthorization();

        group.MapPost("/verify-email", async (VerificationInput input, HttpContext context, AuthService service, CancellationToken ct) =>
            string.IsNullOrEmpty(input.Token) || !await service.VerifyEmailAsync(Current(context), input.Token, ct)
                ? Error("This verification link is invalid or expired.", 400)
                : Results.Ok(new { message = "Email verified." })).RequireAuthorization();

        app.MapGet("/api/admin/dashboard", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { message = "You are signed in.", name = Current(context).Name });
        }).RequireAuthorization();
    }

    private static CmsUser Current(HttpContext context) => (CmsUser)context.Items[typeof(CmsUser)]!;
    private static IResult Error(string message, int status) => Results.Json(new { message }, statusCode: status);
}
