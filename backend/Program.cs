using Diwakar.Cms.Api.Authentication;
using MySqlConnector;
using Diwakar.Cms.Api.Authorization;
using Microsoft.AspNetCore.Http.Features;

if(args.Length>0 && args[0]=="provision-super-admin") { await RbacProvisioningCommand.RunAsync(args); return; }

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false).AddEnvironmentVariables();
builder.AddCmsAuthentication();

builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
            .ExecuteAsync(context);
    }));
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    try { await next(context); }
    catch (Exception error) when (error is MySqlException or IOException or InvalidOperationException)
    {
        app.Logger.LogWarning("Request unavailable ({ErrorType}).", error.GetType().Name);
        if (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.Headers.CacheControl = "no-store";
            await Results.Json(new { message = "The service is temporarily unavailable. Please try again later." }, statusCode: 503).ExecuteAsync(context);
        }
    }
});
app.UseRouting();
app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/api/admin/media")
    {
        var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (size is { IsReadOnly: false }) size.MaxRequestBodySize = Diwakar.Cms.Api.Media.MediaEndpoints.UploadBodyLimit;
    }
    if ((HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method))
        && System.Text.RegularExpressions.Regex.IsMatch(context.Request.Path.Value ?? "", @"^/api/admin/pages/[1-9][0-9]*/sections(?:/[1-9][0-9]*)?$"))
    {
        var size = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (size is { IsReadOnly: false }) size.MaxRequestBodySize = 65536;
    }
    await next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/api/health");
app.MapCmsAuthentication();

app.Run();

public partial class Program { }
