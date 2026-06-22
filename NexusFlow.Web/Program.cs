using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Console;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NexusFlow.AppCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Installation;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Infrastructure;
using NexusFlow.Infrastructure.Hangfire;
using NexusFlow.Infrastructure.Hubs;
using NexusFlow.Infrastructure.Installation;
using NexusFlow.Notification;
using NexusFlow.Web;
using NexusFlow.Web.Installation;
using NexusFlow.Web.Security;
using NexusFlow.Web.Services;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var runtime = InstallationRuntime.Create(builder.Configuration);
var paths = runtime.Paths;
var runtimeOptions = runtime.Options;
var stateStore = runtime.StateStore;
await stateStore.EnsureInitializedAsync();
var secretStore = runtime.SecretStore;
if (!string.IsNullOrWhiteSpace(secretStore.Get(InstallationSecretKeys.RestartRequiredAtUtc)))
{
    await secretStore.RemoveAsync(InstallationSecretKeys.RestartRequiredAtUtc);
    await secretStore.RemoveAsync(InstallationSecretKeys.RestartRequiredReason);
}
var connectionProvider = new InstallationConnectionStringProvider(secretStore);
var initialState = stateStore.Get();

if (initialState.Mode == ApplicationMode.Installed && connectionProvider.GetConnectionString() != null)
{
    try
    {
        var databaseProvisioner = new InstallationDatabaseProvisioner(connectionProvider);
        if ((await databaseProvisioner.GetPendingMigrationsAsync()).Count > 0)
        {
            initialState.Mode = ApplicationMode.UpgradeRequired;
            initialState.LastError = null;
            await stateStore.SaveAsync(initialState);
        }
    }
    catch
    {
        initialState.Mode = ApplicationMode.Faulted;
        initialState.LastError = "The installed database is unavailable. Review server-side logs and configuration.";
        await stateStore.SaveAsync(initialState);
    }
}

var configuredJwtSecret = secretStore.Get(InstallationConnectionStringProvider.JwtSecret)
    ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
var runtimeSettings = new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = connectionProvider.GetConnectionString(),
    ["Hangfire:ConnectionString"] = secretStore.Get(InstallationConnectionStringProvider.HangfireConnectionSecret),
    ["ConnectionStrings:AzureBlobStorage"] = secretStore.Get(InstallationConnectionStringProvider.AzureBlobStorageSecret)
        ?? runtimeOptions.AzureBlobStorageConnectionString,
    ["JwtSettings:Secret"] = configuredJwtSecret,
    ["Syncfusion:LicenseKey"] = secretStore.Get(InstallationConnectionStringProvider.SyncfusionLicenseSecret),
    ["Serilog:WriteTo:1:Args:path"] = Path.Combine(paths.LogsPath, "nexusflow-log-.txt")
};
builder.Configuration.AddInMemoryCollection(runtimeSettings);

builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllersWithViews();
builder.Services.AddAppCore();
builder.Services.AddInstallationInfrastructure(paths, stateStore, secretStore, runtimeOptions);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddNotifications();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IInstallerAccessService, InstallerAccessService>();
builder.Services.AddSingleton<IInstallationPreflightChecker, InstallationPreflightChecker>();
builder.Services.AddSingleton<IApplicationRestartService, ApplicationRestartService>();

var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName($"NexusFlow.ERP.{paths.InstanceId}");
if (runtimeOptions.DataProtectionStoreMode == DataProtectionStoreMode.AzureBlob &&
    !string.IsNullOrWhiteSpace(runtimeOptions.AzureBlobStorageConnectionString))
{
    builder.Services.Configure<KeyManagementOptions>(options =>
    {
        options.XmlRepository = new AzureBlobXmlRepository(
            runtimeOptions.AzureBlobStorageConnectionString,
            runtimeOptions.AzureBlobDataProtectionContainer!,
            runtimeOptions.DataProtectionBlobName!);
    });
}
else
{
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(paths.DataProtectionKeysPath));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("installer", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var shouldRunProductionServices = stateStore.Get().Mode == ApplicationMode.Installed;
if (shouldRunProductionServices)
{
    var hangfireConnection = secretStore.Get(InstallationConnectionStringProvider.HangfireConnectionSecret)
        ?? connectionProvider.GetRequiredConnectionString();
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseConsole()
        .UseSqlServerStorage(hangfireConnection, new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.FromSeconds(15),
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true,
            PrepareSchemaIfNecessary = true
        }));
    builder.Services.AddHangfireServer(options =>
    {
        options.WorkerCount = int.Parse(builder.Configuration["Hangfire:WorkerCount"] ?? "5");
        options.Queues = builder.Configuration.GetSection("Hangfire:Queues").Get<string[]>()
            ?? ["critical", "default", "low"];
        options.ServerName = $"{paths.InstanceId}:{Environment.MachineName}:{Environment.ProcessId}";
    });
    builder.Services.AddScoped<IBackgroundJobService, BackgroundJobService>();
}

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

var syncfusionLicense = secretStore.Get(InstallationConnectionStringProvider.SyncfusionLicenseSecret);
if (!string.IsNullOrWhiteSpace(syncfusionLicense))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(syncfusionLicense);
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    // Local Visual Studio HTTP profiles cannot return a Secure cookie. Production stays HTTPS-only.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.Name = $"NexusFlow.{paths.InstanceId}.Auth";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuredJwtSecret))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT token."
        };
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes.TryAdd("Bearer", securityScheme);
        document.Security = [new OpenApiSecurityRequirement
        {
            { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
        }];
        return Task.CompletedTask;
    });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.HybridPolicy, policy =>
    {
        policy.AuthenticationSchemes.Add(AuthConstants.IdentityScheme);
        policy.AuthenticationSchemes.Add(AuthConstants.JwtScheme);
        policy.RequireAuthenticatedUser();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("NexusFlow API").AddPreferredSecuritySchemes("Bearer"));
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<ApplicationModeMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

if (shouldRunProductionServices)
{
    var dashboardPath = builder.Configuration["Hangfire:DashboardPath"] ?? "/hangfire";
    app.UseHangfireDashboard(dashboardPath, new DashboardOptions
    {
        Authorization = [new HangfireDashboardAuthFilter()],
        DashboardTitle = "NexusFlow Job Dashboard",
        DisplayStorageConnectionString = false,
        StatsPollingInterval = 5000
    });
    RecurringJobsRegistrar.RegisterAll();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "live", instance = paths.InstanceId, profile = runtimeOptions.Profile.ToString() }))
    .AllowAnonymous();
app.MapGet("/health/ready", async (IInstallationStateStore installationState, IServiceScopeFactory scopes) =>
{
    var state = installationState.Get();
    if (state.Mode != ApplicationMode.Installed)
    {
        return Results.Json(new { status = "not-ready", mode = state.Mode.ToString(), detail = state.LastError },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    await using var scope = scopes.CreateAsyncScope();
    var report = await scope.ServiceProvider.GetRequiredService<IInstallationReadinessChecker>().CheckAsync();
    return report.IsReady
        ? Results.Ok(new { status = "ready", checks = report.Checks })
        : Results.Json(new { status = "not-ready", checks = report.Checks }, statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
