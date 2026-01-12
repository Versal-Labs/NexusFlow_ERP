using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NexusFlow.AppCore;
using NexusFlow.Infrastructure;
using NexusFlow.Infrastructure.Persistence;
using NexusFlow.Notification;
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
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddNotifications();
// -----------------------------------

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        // 1. Define the Bearer JWT Scheme
        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter your JWT Token here."
        };

        // 2. Add to Components (Using the safe pattern from your snippet)
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        if (!document.Components.SecuritySchemes.ContainsKey("Bearer"))
        {
            document.Components.SecuritySchemes.Add("Bearer", securityScheme);
        }

        // 3. Create the Security Requirement
        // FIX 1: Use 'OpenApiSecuritySchemeReference' as the Key
        var schemeReference = new OpenApiSecuritySchemeReference("Bearer", document);

        // FIX: The dictionary expects a List<string>, not string[]
        var requirement = new OpenApiSecurityRequirement
        {
            { schemeReference, new List<string>() }
        };

        document.Security = new List<OpenApiSecurityRequirement>
        {
            requirement
        };

        return Task.CompletedTask;
    });
});

// =================================================================
// DUAL AUTHENTICATION SETUP
// =================================================================
builder.Services.AddAuthentication(options =>
{
    // 1. Set the Default to check for Cookies first, then fall back logic
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    // Configuration for the MVC Cookie
    options.LoginPath = "/Account/Login"; // Where to redirect if not logged in
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // crucial for security
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
})
.AddJwtBearer(options =>
{
    // Configuration for Mobile App JWT (Keep your existing code here)
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

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// In Top-Level statements, 'await' works automatically here
using (var scope = app.Services.CreateScope())
{
    var initialiser = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
    await initialiser.InitialiseAsync(); // Runs Migrations
    await initialiser.SeedAsync();       // Inserts Admin User
}

app.Run();
