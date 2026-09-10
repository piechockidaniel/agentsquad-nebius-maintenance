using AgentSquad.Agents.Configuration;
using AgentSquad.Agents.Maintenance;
using AgentSquad.NebiusSandbox;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMaintenanceSquad(builder.Configuration);
builder.Services.AddSingleton<IMaintenanceSandbox, ContreeMaintenanceSandbox>();

var app = builder.Build();

if (app.Environment.IsProduction())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
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

        if (ProductionWebAccess.IsOperatorAuthorized(context.Request.Headers.Authorization, web))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers["WWW-Authenticate"] = ProductionWebAccess.AuthenticationChallenge;
    });
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () =>
    Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }));

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
