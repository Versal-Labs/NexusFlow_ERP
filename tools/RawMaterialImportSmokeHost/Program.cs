using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Identity;
using NexusFlow.IntegrationTests;
using NexusFlow.Web.Security;

// Opt-in local-only harness: real Razor views/controllers/handlers, synthetic disposable SQL Server DB.
// Does NOT start NexusFlow.Web.Program, load appsettings, or access installation credentials.
if (Environment.GetEnvironmentVariable("NEXUSFLOW_RUN_LOCALDB_IMPORT_TESTS") != "1")
    throw new InvalidOperationException("Set NEXUSFLOW_RUN_LOCALDB_IMPORT_TESTS=1 to run this local-only test host.");
var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../NexusFlow.Web/wwwroot"));
// Resolve from project directory when invoked with dotnet run --project from the repository root.
if (!Directory.Exists(webRoot)) webRoot = Path.GetFullPath("NexusFlow.Web/wwwroot");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = [], EnvironmentName = "Development", ApplicationName = typeof(NexusFlow.Web.Controllers.InventoryController).Assembly.FullName,
    WebRootPath = webRoot
});
builder.Configuration.Sources.Clear();
builder.Configuration.AddInMemoryCollection();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls("http://127.0.0.1:5189");
// Only the import workflow is wired in this test host, not payroll, authentication management, etc.
builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
await using var database = new RawMaterialImportTestDatabase();
await database.Initialize();
builder.Configuration["ConnectionStrings:DefaultConnection"] = database.ConnectionString;
database.Configure(builder.Services, addConfiguration: false);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(NexusFlow.Web.Controllers.InventoryController).Assembly);
builder.Services.AddAuthentication(AuthConstants.IdentityScheme)
    .AddCookie(AuthConstants.IdentityScheme, options => {
        options.LoginPath = "/smoke-login";
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
    }).AddCookie(AuthConstants.JwtScheme);
builder.Services.AddAuthorization(options => options.AddPolicy(AuthConstants.HybridPolicy,
    policy => policy.AddAuthenticationSchemes(AuthConstants.IdentityScheme, AuthConstants.JwtScheme).RequireAuthenticatedUser()));
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
var app = builder.Build();
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/smoke-login", async (HttpContext context) => {
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "raw-import-test-user"), new(ClaimTypes.Name, "Local Import Tester") };
    claims.Add(new("Permission", Permissions.Inventory.ViewStock));
    var mode = context.Request.Query["mode"].ToString();
    if (mode is not "viewonly" and not "stockonly") claims.Add(new("Permission", Permissions.MasterData.ManageProducts));
    if (mode is not "viewonly" and not "productonly") claims.Add(new("Permission", Permissions.Inventory.AdjustStock));
    await context.SignInAsync(AuthConstants.IdentityScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthConstants.IdentityScheme)));
    return Results.Redirect("/Inventory/RawMaterialImport");
}).AllowAnonymous();
app.MapGet("/smoke-state", async () => {
    await using var db = database.Open();
    return Results.Ok(new { products = await db.Products.CountAsync(), layers = await db.StockLayers.CountAsync(),
        transactions = await db.StockTransactions.CountAsync(), journals = await db.JournalEntries.CountAsync() });
});
app.MapPost("/smoke-stop", (IHostApplicationLifetime lifetime) => {
    lifetime.StopApplication();
    return Results.Ok("Stopping local test host and deleting its synthetic database.");
});
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
Console.WriteLine("Synthetic import test host: http://127.0.0.1:5189/smoke-login. POST /smoke-stop for graceful database cleanup.");
await app.RunAsync();
