using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using StickyNotesClassic.App.Services;
using StickyNotesClassic.App.Services.Hotkeys;
using StickyNotesClassic.App.ViewModels;
using StickyNotesClassic.Core.Data;
using StickyNotesClassic.Core.Repositories;
using StickyNotesClassic.Core.Services;
using StickyNotesClassic.Core.Utilities;

namespace StickyNotesClassic.App;

/// <summary>
/// Configures dependency injection services for the application.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceCollection ConfigureServices(this IServiceCollection services)
    {
        // Configure Serilog
        var logPath = Path.Combine(AppPathHelper.GetLogsDirectory(), "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // Core infrastructure services
        services.AddSingleton<NotesDbContext>();
        services.AddSingleton<INotesRepository, NotesRepository>();
        services.AddSingleton<AutosaveService>();

        // App-specific services
        services.AddSingleton<ThemeService>();
        services.AddSingleton<BackupService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IHotkeyRegistrar>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            if (OperatingSystem.IsWindows())
            {
                return new WindowsHotkeyRegistrar(loggerFactory.CreateLogger<WindowsHotkeyRegistrar>());
            }

            if (OperatingSystem.IsLinux())
            {
                return new LinuxHotkeyRegistrar(loggerFactory.CreateLogger<LinuxHotkeyRegistrar>());
            }

            if (OperatingSystem.IsMacOS())
            {
                return new MacHotkeyRegistrar(loggerFactory.CreateLogger<MacHotkeyRegistrar>());
            }

            return new NullHotkeyRegistrar(loggerFactory.CreateLogger<NullHotkeyRegistrar>());
        });
        services.AddSingleton<HotkeyService>();

        // ViewModels are transient (created fresh for each window)
        services.AddTransient<SettingsViewModel>();

        return services;
    }
}
