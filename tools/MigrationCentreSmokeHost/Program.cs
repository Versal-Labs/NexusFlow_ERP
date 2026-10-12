using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Migration;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Identity;
using NexusFlow.Infrastructure.Services;
using NexusFlow.IntegrationTests;
using NexusFlow.Web.Security;

// Real MVC and production application DI, synthetic GUID database, loopback only, opt-in.
if (Environment.GetEnvironmentVariable("NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS") != "1") throw new InvalidOperationException("Set NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS=1 for this disposable harness.");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development", ApplicationName = typeof(NexusFlow.Web.Controllers.MigrationController).Assembly.FullName,
    WebRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../NexusFlow.Web/wwwroot")) });
builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(); builder.Logging.SetMinimumLevel(LogLevel.Warning); builder.WebHost.UseUrls("http://127.0.0.1:5191"); builder.Host.UseDefaultServiceProvider(x => x.ValidateOnBuild = false);
await using var database = new CustomerMigrationTestDatabase(); await database.Initialize();
builder.Configuration["ConnectionStrings:DefaultConnection"] = database.ConnectionString; database.Configure(builder.Services, addConfiguration: false);
builder.Services.AddScoped<IMigrationFileReader, MigrationFileReader>(); builder.Services.AddSingleton<IGlobalStorageCoordinator, SyntheticStorage>(); builder.Services.AddSingleton<IBackgroundJobService, SyntheticJobs>();
builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUserService, CurrentUserService>(); builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(NexusFlow.Web.Controllers.MigrationController).Assembly);
builder.Services.AddAuthentication(AuthConstants.IdentityScheme).AddCookie(AuthConstants.IdentityScheme, options => { options.LoginPath = "/smoke-login"; options.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; }; }).AddCookie(AuthConstants.JwtScheme);
builder.Services.AddAuthorization(options => options.AddPolicy(AuthConstants.HybridPolicy, p => p.AddAuthenticationSchemes(AuthConstants.IdentityScheme, AuthConstants.JwtScheme).RequireAuthenticatedUser()));
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>(); builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
var app = builder.Build(); app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/smoke-login", async (HttpContext context) => {
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "migration-tester"), new(ClaimTypes.Name, "Synthetic Migration Tester") };
    var mode = context.Request.Query["mode"].ToString();
    if (mode == "viewonly") claims.Add(new("Permission", Permissions.Migration.View));
    else if (mode == "prepareonly") { claims.Add(new("Permission", Permissions.Migration.View)); claims.Add(new("Permission", Permissions.Migration.Prepare)); }
    else if (mode == "unrelated") claims.Add(new("Permission", Permissions.Treasury.ViewReceipts));
    else claims.Add(new("Permission", Permissions.SuperAdmin));
    await context.SignInAsync(AuthConstants.IdentityScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthConstants.IdentityScheme))); return Results.Redirect("/Migration");
}).AllowAnonymous();
app.MapGet("/smoke-state", async () => { await using var db = database.Open(); return Results.Ok(new { projects = await db.MigrationProjects.CountAsync(), items = await db.CustomerOpeningItems.CountAsync(), journals = await db.JournalEntries.CountAsync(), units = await db.MigrationUnits.CountAsync(), runs = await db.MigrationRuns.Select(x => x.Status).ToListAsync() }); });
app.MapPost("/smoke-stop", (IHostApplicationLifetime lifetime) => { lifetime.StopApplication(); return Results.Ok("Stopping; synthetic database will be deleted."); });
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
Console.WriteLine("Migration Centre smoke host: http://127.0.0.1:5191/smoke-login; POST /smoke-stop cleans up owned database."); await app.RunAsync();

sealed class SyntheticStorage : IGlobalStorageCoordinator
{
    readonly ConcurrentDictionary<string, byte[]> files = new();
    public async Task<string> SaveFileSecurelyAsync(Stream stream, string name, string folder, string type, CancellationToken ct = default) { using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct); var key = Guid.NewGuid().ToString("N"); files[key] = memory.ToArray(); return key; }
    public Task<(Stream Stream, string ContentType)> RetrieveFileAsync(string key, CancellationToken ct = default) => Task.FromResult(((Stream)new MemoryStream(files[key]), "application/octet-stream"));
    public Task DeleteFileAsync(string key, CancellationToken ct = default) { files.TryRemove(key, out _); return Task.CompletedTask; }
}
sealed class SyntheticJobs(IServiceScopeFactory scopes) : IBackgroundJobService
{
    readonly ConcurrentDictionary<string, string> states = new();
    public string EnqueueAsync<T>(Expression<Func<T, Task>> call)
    {
        var id = Guid.NewGuid().ToString("N"); states[id] = "Enqueued";
        _ = Task.Run(async () => { states[id] = "Processing"; try { using var scope = scopes.CreateScope(); await call.Compile()((T)scope.ServiceProvider.GetRequiredService(typeof(T))); states[id] = "Succeeded"; } catch { states[id] = "Failed"; } }); return id;
    }
    public string? GetState(string id) => states.GetValueOrDefault(id);
    public string Enqueue<T>(Expression<Action<T>> call) => throw new NotSupportedException();
    public string Schedule<T>(Expression<Action<T>> call, TimeSpan delay) => throw new NotSupportedException();
    public void AddOrUpdateRecurring<T>(string id, Expression<Action<T>> call, string cron, string queue = "default") => throw new NotSupportedException();
    public void RemoveRecurring(string id) => throw new NotSupportedException();
}
