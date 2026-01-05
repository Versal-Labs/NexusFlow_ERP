using NexusFlow.AppCore;
using NexusFlow.Infrastructure;
using NexusFlow.Notification;
using Serilog;
public partial class Program
{
    private static void Main(string[] args)
    {
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

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseSerilogRequestLogging();

        app.UseHttpsRedirection();
        app.UseRouting();

        app.UseAuthorization();

        app.MapStaticAssets();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();


        app.Run();
    }
}