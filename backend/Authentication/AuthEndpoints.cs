using System.Globalization;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Diwakar.Cms.Api.Authorization;

namespace Diwakar.Cms.Api.Authentication;

public sealed record CreateAdminInput(string? Name, string? Email, string? Password, string? PasswordConfirmation);
public sealed record RoleAssignmentInput(string? RoleId);
public sealed record RolePermissionsInput(string[]? Permissions);

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

        app.MapGet("/api/auth/authorization", async (HttpContext context, RbacStore rbac, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var state = await rbac.GetAsync(Current(context).Id, ct);
            return Results.Ok(new { roles = state.Roles, permissions = state.Permissions.Order(StringComparer.Ordinal), isSuperAdmin = state.IsSuperAdmin,
                canAccessAdmin = state.Allows("admin.access"), canViewUsers = state.Allows("users.view"), canAssignRoles = state.Allows("users.assign_roles"),
                canViewRoles = state.Allows("roles.view"), canManagePermissions = state.Allows("roles.manage_permissions") });
        }).RequireAuthorization();

        var admin = app.MapGroup("/api/admin").RequireAuthorization().AddEndpointFilter(async (invocation, next) =>
        {
            var context=invocation.HttpContext; context.Response.Headers.CacheControl="no-cache, no-store"; context.Response.Headers.Pragma="no-cache";
            if(!HttpMethods.IsGet(context.Request.Method)) { try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); } catch(AntiforgeryValidationException) { return Results.Json(new { message="The request could not be verified. Refresh the page and try again." },statusCode:400); } }
            return await next(invocation);
        });
        admin.AddEndpointFilter(async (invocation,next) => { var s=await invocation.HttpContext.RequestServices.GetRequiredService<RbacStore>().GetAsync(Current(invocation.HttpContext).Id,invocation.HttpContext.RequestAborted); return s.Allows("admin.access")?await next(invocation):Results.Json(new {message="You do not have access to this area."},statusCode:403); });
        admin.MapGet("/dashboard", async (HttpContext context,RbacStore rbac,CancellationToken ct) => Results.Ok(await rbac.DashboardAsync(ct))).RequireAuthorization();
        admin.MapGet("/users", async (HttpContext context,RbacStore rbac,CancellationToken ct) => { var s=await rbac.GetAsync(Current(context).Id,ct); return s.Allows("users.view")?Results.Ok(new { users=await rbac.UsersAsync(ct), roles=await rbac.AssignmentRolesAsync(ct) }):Results.Json(new {message="You do not have permission to view users."},statusCode:403); });
        admin.MapPost("/users", async (CreateAdminInput input, HttpContext context, RbacStore rbac, CancellationToken ct) =>
        {
            var result = await rbac.CreateAdminAsync(Current(context).Id, input.Name, input.Email,
                input.Password, input.PasswordConfirmation, ct);
            return result.Status switch
            {
                CreateAdminStatus.Created => Results.Json(new { message = "Admin created.", user = new
                {
                    id = result.Id!.Value.ToString(CultureInfo.InvariantCulture), result.Name, result.Email,
                    emailVerified = false, role = "Admin"
                } }, statusCode: 201),
                CreateAdminStatus.Invalid => Error("Enter a valid name, email, and matching password of at least eight characters.", 400),
                CreateAdminStatus.Duplicate => Error("An account with this email already exists.", 409),
                _ => Error("Only a Super Admin can create Admin accounts.", 403)
            };
        });
        admin.MapPatch("/users/{id}/role", async (string id,RoleAssignmentInput input,HttpContext context,RbacStore rbac,CancellationToken ct) =>
        {
            if(!ulong.TryParse(id,out var target)||target==0||!ulong.TryParse(input.RoleId,out var role)||role==0)return Results.BadRequest(new {message="Choose a valid user and role."});
            return await rbac.AssignRoleAsync(Current(context).Id,target,role,ct)?Results.Ok(new {message="User role updated."}):Results.Json(new {message="The assignment is not permitted or conflicts with Super Admin protection."},statusCode:403);
        });
        admin.MapGet("/roles", async (HttpContext context,RbacStore rbac,CancellationToken ct) => { var s=await rbac.GetAsync(Current(context).Id,ct); return s.Allows("roles.view")?Results.Ok(await rbac.RolesAsync(ct)):Results.Json(new {message="You do not have permission to view roles."},statusCode:403); });
        admin.MapPatch("/roles/{id}/permissions", async (string id,RolePermissionsInput input,HttpContext context,RbacStore rbac,CancellationToken ct) =>
        {
            if(!ulong.TryParse(id,out var role)||role==0||input.Permissions is null)return Results.BadRequest(new {message="Provide a valid role and permissions list."});
            return await rbac.UpdateRolePermissionsAsync(Current(context).Id,role,input.Permissions,ct)?Results.Ok(new {message="Role permissions updated."}):Results.Json(new {message="The update is not permitted or contains unsupported permissions."},statusCode:403);
        });
    }

    private static CmsUser Current(HttpContext context) => (CmsUser)context.Items[typeof(CmsUser)]!;
    private static IResult Error(string message, int status) => Results.Json(new { message }, statusCode: status);
}
