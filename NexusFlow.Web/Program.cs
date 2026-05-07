
using Hangfire;
using Hangfire.Console;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NexusFlow.AppCore;
using NexusFlow.AppCore.Constants;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Infrastructure;
using NexusFlow.Infrastructure.Hangfire;
using NexusFlow.Infrastructure.Hubs;
using NexusFlow.Infrastructure.Persistence;
using NexusFlow.Notification;
using NexusFlow.Web;
using NexusFlow.Web.Security;
using Scalar.AspNetCore;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container.
builder.Services.AddControllersWithViews();

// --- CLEAN ARCHITECTURE DI SETUP ---
builder.Services.AddAppCore();

// IMPORTANT: This adds AddIdentity<ApplicationUser...>, which sets the Default Scheme to "Identity.Application"
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddNotifications();

// --- HANGFIRE CONFIGURATION ---
var hangfireConnection = builder.Configuration["Hangfire:ConnectionString"]
    ?? builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseConsole()                          // Enables rich job logs in dashboard
    .UseSqlServerStorage(hangfireConnection, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(15),
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true,   // Required for multi-server setups
        PrepareSchemaIfNecessary = true,   // Auto-creates Hangfire tables
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = int.Parse(
        builder.Configuration["Hangfire:WorkerCount"] ?? "5");
    options.Queues = builder.Configuration
        .GetSection("Hangfire:Queues").Get<string[]>()
        ?? new[] { "critical", "default", "low" };
    options.ServerName = $"{Environment.MachineName}:{Environment.ProcessId}";
});

// Register the job service abstraction
builder.Services.AddScoped<IBackgroundJobService, BackgroundJobService>();
// --- END HANGFIRE CONFIGURATION ---

builder.Services.AddSignalR();

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Add this at the very beginning of your Program.cs
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1JHaF5cWWdCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdlWXxcdXRdRmdZV0Z0XURWYEo=");
// -----------------------------------

// =================================================================
// 1. CONFIGURE THE IDENTITY COOKIE (Fixes the Login Loop)
// =================================================================
// Since AddInfrastructure already added Identity, we just configure its cookie here.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; // Where to redirect if not logged in
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

    // --- PRODUCTION FIX START ---
    // Prevent 302 Redirects for API calls (return 401 instead)
    options.Events.OnRedirectToLogin = context =>
    {
        // Check if the request is for an API endpoint
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        // Otherwise, do the normal redirect for MVC views
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    // --- PRODUCTION FIX END ---
});

// =================================================================
// 2. ADD JWT AUTHENTICATION (For Mobile/API)
// =================================================================
// We append JwtBearer to the existing AuthenticationBuilder
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Secret"]!))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // 1. Look for "access_token" in the query string
                var accessToken = context.Request.Query["access_token"];

                // 2. Check if the request is for our Hub path
                var path = context.HttpContext.Request.Path;

                // If token exists AND request is for a Hub...
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs/notifications")))
                {
                    // 3. Read the token from the query string instead of the header
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

// OpenAPI / Swagger Configuration
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
            Description = "Enter your JWT Token here."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        if (!document.Components.SecuritySchemes.ContainsKey("Bearer"))
        {
            document.Components.SecuritySchemes.Add("Bearer", securityScheme);
        }

        var schemeReference = new OpenApiSecuritySchemeReference("Bearer", document);
        var requirement = new OpenApiSecurityRequirement
        {
            { schemeReference, new List<string>() }
        };

        document.Security = new List<OpenApiSecurityRequirement> { requirement };

        return Task.CompletedTask;
    });
});

builder.Services.AddAuthorization(options =>
{
    // Create a policy that accepts EITHER Cookie OR JWT
    options.AddPolicy("HybridPolicy", policy =>
    {
        policy.AuthenticationSchemes.Add(AuthConstants.IdentityScheme);
        policy.AuthenticationSchemes.Add(AuthConstants.JwtScheme);
        policy.RequireAuthenticatedUser();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("NexusFlow API")
            .AddPreferredSecuritySchemes("Bearer");
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();
app.UseRouting();

// 3. MIDDLEWARE ORDER IS CRITICAL
app.UseAuthentication();
app.UseAuthorization();

// --- HANGFIRE DASHBOARD ---
var dashboardPath = builder.Configuration["Hangfire:DashboardPath"] ?? "/hangfire";

app.UseHangfireDashboard(dashboardPath, new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthFilter() },
    DashboardTitle = "NexusFlow — Job Dashboard",
    DisplayStorageConnectionString = false,   // Never expose connection strings
    StatsPollingInterval = 5000,              // Refresh every 5 seconds
    IsReadOnlyFunc = context =>
    {
        // Optionally make dashboard read-only for non-super-admins
        return false;
    }
});

// Register all recurring jobs
RecurringJobsRegistrar.RegisterAll();
// --- END HANGFIRE DASHBOARD ---

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
    await initialiser.InitialiseAsync();
    await initialiser.SeedAsync();
}

//Hubs
app.MapHub<NotificationHub>("/hubs/notifications");

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<IErpDbContext>();

        // Ensure database is created/migrated first
        // await ((DbContext)context).Database.MigrateAsync(); 

        // Define the path to your JSON file
        var env = services.GetRequiredService<IWebHostEnvironment>();
        var locationJsonPath = Path.Combine(env.ContentRootPath, "SeedData", "sri_lanka_cities.json");

        // Execute the Location Seeder
        await NexusFlow.Infrastructure.Data.Seeders.LocationSeeder.SeedAsync(context, locationJsonPath);

        // (You can add your Bank Seeder here as well!)
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the Master Data.");
    }
}

app.Run();