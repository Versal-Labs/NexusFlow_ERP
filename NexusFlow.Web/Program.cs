using Microsoft.IdentityModel.Tokens;
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

builder.Services.AddOpenApi();

// 1. SETUP DUAL AUTHENTICATION (Cookie + JWT)
builder.Services.AddAuthentication(options =>
{
    // Default schemes can remain Cookies for MVC if you prefer
    // But we configure JWT here so API Controllers can use [Authorize(AuthenticationSchemes = "Bearer")]
})
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("NexusFlow API");
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
