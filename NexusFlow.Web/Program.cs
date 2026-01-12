using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NexusFlow.AppCore;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Infrastructure;
using NexusFlow.Infrastructure.Hubs;
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

// IMPORTANT: This adds AddIdentity<ApplicationUser...>, which sets the Default Scheme to "Identity.Application"
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddNotifications();

builder.Services.AddSignalR();
// -----------------------------------

// =================================================================
// 1. CONFIGURE THE IDENTITY COOKIE (Fixes the Login Loop)
// =================================================================
// Since AddInfrastructure already added Identity, we just configure its cookie here.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; // Where to redirect if not logged in
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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

app.Run();