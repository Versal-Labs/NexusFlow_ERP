using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Features.Finance.CustomerMigration;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Infrastructure.Identity;
using NexusFlow.IntegrationTests;
using NexusFlow.Web.Security;

// Opt-in harness, bound to loopback, using real controllers/views and synthetic disposable SQL data.
// No installation secrets or application startup/configuration are loaded.
if (Environment.GetEnvironmentVariable("NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS") != "1") throw new InvalidOperationException("Set NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS=1 for the isolated local harness.");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development",
    ApplicationName = typeof(NexusFlow.Web.Controllers.FinanceController).Assembly.FullName, WebRootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../NexusFlow.Web/wwwroot")) });
builder.Configuration.Sources.Clear(); builder.Configuration.AddInMemoryCollection(); builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls("http://127.0.0.1:5190"); builder.Host.UseDefaultServiceProvider(options => options.ValidateOnBuild = false);
await using var database = new CustomerMigrationTestDatabase(); await database.Initialize();
builder.Configuration["ConnectionStrings:DefaultConnection"] = database.ConnectionString; database.Configure(builder.Services, addConfiguration: false);
builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(NexusFlow.Web.Controllers.FinanceController).Assembly);
builder.Services.AddAuthentication(AuthConstants.IdentityScheme).AddCookie(AuthConstants.IdentityScheme, options => {
    options.LoginPath = "/smoke-login"; options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
}).AddCookie(AuthConstants.JwtScheme);
builder.Services.AddAuthorization(options => options.AddPolicy(AuthConstants.HybridPolicy, policy => policy.AddAuthenticationSchemes(AuthConstants.IdentityScheme, AuthConstants.JwtScheme).RequireAuthenticatedUser()));
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>(); builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
var app = builder.Build(); app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/smoke-login", async (HttpContext context) => {
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "customer-migration-tester"), new(ClaimTypes.Name, "Local Migration Tester") };
    var mode = context.Request.Query["mode"].ToString();
    if (mode == "viewonly") claims.Add(new("Permission", Permissions.Treasury.ViewReceipts));
    else if (mode == "financeonly") claims.Add(new("Permission", Permissions.Finance.ManageAccounts));
    else if (mode == "treasuryonly") claims.Add(new("Permission", Permissions.Treasury.ManageCheques));
    else claims.Add(new("Permission", Permissions.SuperAdmin));
    await context.SignInAsync(AuthConstants.IdentityScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthConstants.IdentityScheme)));
    return Results.Redirect("/Finance/CustomerMigration");
}).AllowAnonymous();
app.MapGet("/smoke-fixture/{kind}", (string kind) => { var files = CustomerMigrationFixtures.Files(); return Results.File(kind == "outstanding" ? files.Outstanding : files.Cheques, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", kind + ".xlsx"); });
app.MapPost("/smoke-draft", async (CustomerMigrationService migration) => { var files = CustomerMigrationFixtures.Files(); return Results.Ok(await migration.CreateAsync(files.Outstanding, "outstanding.xlsx", files.Cheques, "cheques.xlsx", new(2026,10,1), new(2026,9,30), default)); });
app.MapGet("/smoke-state", async () => { await using var db = database.Open(); return Results.Ok(new { items = await db.CustomerOpeningItems.CountAsync(), cheques = await db.ChequeRegisters.CountAsync(), journals = await db.JournalEntries.CountAsync(), receipts = await db.PaymentTransactions.CountAsync() }); });
app.MapPost("/smoke-stop", (IHostApplicationLifetime lifetime) => { lifetime.StopApplication(); return Results.Ok("Stopping and deleting synthetic database."); });
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
Console.WriteLine("Customer migration test host: http://127.0.0.1:5190/smoke-login. POST /smoke-stop for cleanup.");
await app.RunAsync();
