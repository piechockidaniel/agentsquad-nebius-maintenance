using AgentSquad.Agents.Configuration;
using AgentSquad.Agents.Maintenance;
using AgentSquad.Host;
using AgentSquad.NebiusSandbox;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMaintenanceSquad(builder.Configuration);
builder.Services.AddSingleton<IMaintenanceSandbox, ContreeMaintenanceSandbox>();
builder.Services.AddSingleton<OperatorSessionStore>();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/operator/sign-in"))
        {
            await next(context);
            return;
        }

        var web = context.RequestServices.GetRequiredService<IOptions<AgentSquadOptions>>().Value.Web;
        if (!ProductionWebAccess.IsOperatorConfigured(web))
        {
            await Results.Problem(
                "Console is unavailable until AgentSquad__Web__OperatorUsername and AgentSquad__Web__OperatorPassword are configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable).ExecuteAsync(context);
            return;
        }

        var sessions = context.RequestServices.GetRequiredService<OperatorSessionStore>();
        if (sessions.IsActive(context.Request.Cookies[OperatorSessionStore.CookieName], DateTimeOffset.UtcNow))
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/maintenance"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Redirect("/operator/sign-in");
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () =>
    Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

app.MapGet("/operator/sign-in", () =>
    Results.Content(OperatorSignInPage.Html, "text/html"));

app.MapPost("/operator/sign-in", (OperatorSignInRequest request, IOptions<AgentSquadOptions> options,
    OperatorSessionStore sessions, HttpResponse response) =>
{
    var web = options.Value.Web;
    if (!ProductionWebAccess.IsOperatorConfigured(web))
    {
        return Results.Problem(
            "Console is unavailable until AgentSquad__Web__OperatorUsername and AgentSquad__Web__OperatorPassword are configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!ProductionWebAccess.AreOperatorCredentialsValid(request.Username, request.Password, web))
    {
        return Results.Unauthorized();
    }

    var now = DateTimeOffset.UtcNow;
    response.Cookies.Append(OperatorSessionStore.CookieName, sessions.Create(now), new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Strict,
        Secure = true,
        Expires = now.Add(OperatorSessionStore.SessionLifetime)
    });

    return Results.NoContent();
});

app.MapPost("/operator/sign-out", (HttpRequest request, HttpResponse response, OperatorSessionStore sessions) =>
{
    sessions.Revoke(request.Cookies[OperatorSessionStore.CookieName]);
    response.Cookies.Delete(OperatorSessionStore.CookieName, new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Strict,
        Secure = true
    });
    return Results.NoContent();
});

var maintenance = app.MapGroup("/maintenance");

maintenance.AddEndpointFilter(async (context, next) =>
{
    var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<AgentSquadOptions>>().Value.Web;
    if (string.IsNullOrWhiteSpace(options.AccessToken))
    {
        return app.Environment.IsDevelopment()
            ? await next(context)
            : Results.Problem("Maintenance API is unavailable until AgentSquad__Web__AccessToken is configured.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var supplied = context.HttpContext.Request.Headers["X-AgentSquad-Access-Token"].ToString();
    var expectedBytes = Encoding.UTF8.GetBytes(options.AccessToken);
    var suppliedBytes = Encoding.UTF8.GetBytes(supplied);

    return suppliedBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(suppliedBytes,
        expectedBytes)
        ? await next(context)
        : Results.Unauthorized();
});

maintenance.MapGet("/preflight", async (MaintenanceService service, CancellationToken ct) =>
    Results.Ok(await service.GetPreflightAsync(ct)));

maintenance.MapGet("/scenarios", () =>
    Results.Ok(MaintenanceScenarioCatalog.All));

maintenance.MapGet("/manual-checks", () =>
    Results.Ok(ManualSandboxCheckCatalog.All.Select(check => new
    {
        check.Id,
        check.Title,
        check.Description,
        check.ExpectedResult
    })));

maintenance.MapPost("/runs", (string? scenario, MaintenanceService service) =>
{
    var result = service.Start(MaintenanceRunTriggers.Manual, scenario);
    return !result.Enabled ? Results.Problem(result.Error, statusCode: 503)
        : result.StatusCode is { } statusCode ? Results.Problem(result.Error, statusCode: statusCode)
        : !result.Started ? Results.Conflict(new { error = result.Error })
        : Results.Accepted($"/maintenance/runs/{result.Run!.Id}", result.Run);
});

maintenance.MapGet("/runs", (MaintenanceService service, int? limit) =>
    Results.Ok(service.Recent(limit)));

maintenance.MapGet("/runs/{id}", (string id, MaintenanceService service) =>
    service.Get(id) is { } run ? Results.Ok(run) : Results.NotFound());

maintenance.MapPost("/runs/{id}/manual-checks/{checkId}", (string id, string checkId, MaintenanceService service) =>
{
    var result = service.StartManualCheck(id, checkId);
    return !result.Enabled ? Results.Problem(result.Error, statusCode: StatusCodes.Status503ServiceUnavailable)
        : result.StatusCode is 404 ? Results.NotFound(new { error = result.Error })
        : result.StatusCode is { } statusCode ? Results.Problem(result.Error, statusCode: statusCode)
        : !result.Started ? Results.Conflict(new { error = result.Error })
        : Results.Accepted($"/maintenance/runs/{id}", result.Check);
});

app.Run();

internal sealed record OperatorSignInRequest(string? Username, string? Password);
