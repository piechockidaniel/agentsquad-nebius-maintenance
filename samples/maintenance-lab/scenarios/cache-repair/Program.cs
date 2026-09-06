using MaintenanceLab;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<WorkItemCache>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/work-items/{id}", (string id, WorkItemCache cache) =>
    Results.Ok(new { id, title = cache.GetTitle(id) }));
app.Run();

public partial class Program;
