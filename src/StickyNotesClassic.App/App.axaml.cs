using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StickyNotesClassic.Core.Data;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Repositories;
using StickyNotesClassic.Core.Services;
using StickyNotesClassic.App.Services;
using StickyNotesClassic.App.ViewModels;
using StickyNotesClassic.App.Views;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StickyNotesClassic.App;

public partial class App : Application
{
    private IServiceProvider? _services;
    private ILogger<App>? _logger;
    private readonly List<NoteWindow> _openWindows = new();

    /// <summary>
    /// Gets the BackupService instance for use by SettingsWindow.
    /// </summary>
    public BackupService? BackupService => _services?.GetService<BackupService>();

    /// <summary>
    /// Event raised when application settings are saved.
    /// </summary>
    public static event EventHandler? SettingsChanged;

    /// <summary>
    /// Raises the SettingsChanged event to notify all subscribers.
    /// </summary>
    public static void RaiseSettingsChanged()
    {
        SettingsChanged?.Invoke(null, EventArgs.Empty);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Build dependency injection container
            var services = new ServiceCollection();
            services.ConfigureServices();
            _services = services.BuildServiceProvider();
            
            // Get logger after DI container is built
            _logger = _services.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("Application framework initialization completed");
            _logger.LogInformation("Services initialized with dependency injection container");

            // Set up global exception handlers
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            _logger.LogInformation("Global exception handlers registered");

            // Initialize database
            Task.Run(async () =>
            {
                try
                {
                    var dbContext = _services!.GetRequiredService<NotesDbContext>();
                    var repository = _services.GetRequiredService<INotesRepository>();
                    var backupService = _services.GetService<BackupService>();
                    
                    _logger!.LogInformation("Initializing database");
                    await dbContext.InitializeAsync();
                    _logger.LogInformation("Database initialized successfully");
                    
                    // Load settings and schedule auto-backup
                    _logger.LogInformation("Loading application settings");
                    var settings = await repository.GetSettingsAsync();
                    _logger.LogInformation("Settings loaded. AutoBackup: {AutoBackupEnabled}", settings.AutoBackupEnabled);
                    
                    if (settings.AutoBackupEnabled && backupService != null)
                    {
                        backupService.ScheduleDailyBackup(settings.AutoBackupRetentionDays);
                        _logger.LogInformation("Auto-backup scheduled for {RetentionDays} days retention", settings.AutoBackupRetentionDays);
                    }
                    
                    _logger.LogInformation("Loading and showing notes");
                    await LoadAndShowNotesAsync();
                    _logger.LogInformation("Notes loaded and windows created successfully");
                }
                catch (Exception ex)
                {
                    _logger!.LogError(ex, "Fatal error during application initialization");
                }
            });

            // Handle shutdown
            desktop.ShutdownRequested += OnShutdownRequested;
            
            // Subscribe to settings changes to update note colors
            SettingsChanged += OnAppSettingsChanged;
        }
        else
        {
            // This shouldn't happen for desktop app, but log it if it does
            System.Diagnostics.Debug.WriteLine("Warning: Not a desktop application lifetime!");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        try
        {
            _logger?.LogInformation("Settings changed event triggered");
            
            var repository = _services?.GetService<INotesRepository>();
            if (repository == null)
            {
                _logger?.LogWarning("Repository not available, cannot apply settings changes");
                return;
            }
                
            // Get the new default color
            var settings = await repository.GetSettingsAsync();
            var newDefaultColor = settings.DefaultNoteColor;
            
            _logger?.LogInformation("Applying new default color {Color} to {Count} open windows", 
                newDefaultColor, _openWindows.Count);
        
            // Update all open note windows to use the new color
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var window in _openWindows)
                {
                    if (window.DataContext is NoteWindowViewModel vm)
                    {
                        _logger?.LogDebug("Updating note {NoteId} color from {OldColor} to {NewColor}", 
                            vm.NoteId, vm.Color, newDefaultColor);
                        vm.Color = newDefaultColor;
                    }
                }
            });
            
            _logger?.LogInformation("Color update complete for all windows");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply settings changes");
        }
    }

    private async Task LoadAndShowNotesAsync()
    {
        var repository = _services?.GetRequiredService<INotesRepository>();
        var autosaveService = _services?.GetRequiredService<AutosaveService>();
        var themeService = _services?.GetRequiredService<ThemeService>();
        
        if (repository == null || autosaveService == null || themeService == null)
        {
            _logger?.LogError("Required services are null, cannot load notes");
            return;
        }

        _logger?.LogInformation("Fetching all active notes from database");
        var notes = await repository.GetAllActiveNotesAsync();
        _logger?.LogInformation("Found {Count} active notes", notes.Count);

        // If no notes exist, create a default yellow note
        if (notes.Count == 0)
        {
            _logger?.LogInformation("No notes found, creating default note");
            var settings = await repository.GetSettingsAsync();
            var defaultNote = Note.CreateNew(settings.DefaultNoteColor);
            await repository.UpsertNoteAsync(defaultNote);
            notes.Add(defaultNote);
            _logger?.LogInformation("Default note created with color {Color}", settings.DefaultNoteColor);
        }

        // Create a window for each note on UI thread
        _logger?.LogInformation("Creating windows on UI thread");
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var note in notes)
            {
                _logger?.LogDebug("Creating window for note {NoteId}", note.Id);
                CreateNoteWindow(note, autosaveService, themeService, repository);
            }
            _logger?.LogInformation("All windows created successfully");
        });
    }

    private void CreateNoteWindow(Note note, AutosaveService autosaveService, ThemeService themeService, INotesRepository repository)
    {
        var logger = _services?.GetRequiredService<ILogger<NoteWindowViewModel>>();
        if (logger == null)
        {
            _logger?.LogError("Failed to resolve ILogger<NoteWindowViewModel>");
            return;
        }

        var viewModel = new NoteWindowViewModel(note, autosaveService, themeService, repository, logger);
        var window = new NoteWindow
        {
            DataContext = viewModel,
            Width = note.Width,
            Height = note.Height
        };

        // Position window (clamp to screen if needed)
        if (note.X >= 0 && note.Y >= 0)
        {
            // Clamp to screen bounds to prevent off-screen notes
            var screens = window.Screens;
            var targetScreen = screens.ScreenFromPoint(new Avalonia.PixelPoint((int)note.X, (int)note.Y)) 
                              ?? screens.Primary;

            if (targetScreen != null)
            {
                var workArea = targetScreen.WorkingArea;
                
                // Ensure window is at least partially visible
                var clampedX = Math.Max(workArea.X, Math.Min(note.X, workArea.Right - 100)); // Keep at least 100px visible
                var clampedY = Math.Max(workArea.Y, Math.Min(note.Y, workArea.Bottom - 50));  // Keep at least 50px visible
                
                window.Position = new Avalonia.PixelPoint((int)clampedX, (int)clampedY);
            }
            else
            {
                window.Position = new Avalonia.PixelPoint((int)note.X, (int)note.Y);
            }
        }

        // Handle window events
        viewModel.RequestCreateNewNote += (s, e) => OnCreateNewNote();
        viewModel.RequestClose += (s, e) => OnCloseNoteWindow(window, note);

        window.Closed += (s, e) => OnWindowClosed(window);

        _openWindows.Add(window);
        window.Show();
    }

    /// <summary>
    /// Opens the Settings window.
    /// </summary>
    public async Task OpenSettingsWindowAsync()
    {
        var repository = _services?.GetService<INotesRepository>();
        if (repository == null)
        {
            _logger?.LogWarning("Repository not available, cannot open settings");
            return;
        }

        var logger = _services.GetRequiredService<ILogger<SettingsViewModel>>();
        var settingsVm = new SettingsViewModel(repository, logger);
        var settingsWindow = new SettingsWindow
        {
            DataContext = settingsVm
        };

        // Show as dialog (modal)
        var mainWindow = _openWindows.FirstOrDefault();
        if (mainWindow != null)
        {
            await settingsWindow.ShowDialog(mainWindow);
        }
        else
        {
            settingsWindow.Show();
        }
    }

    /// <summary>
    /// Reloads all notes from database and creates windows for new ones.
    /// Used after import to display imported notes.
    /// </summary>
    public async Task ReloadNotesAsync()
    {
        var repository = _services?.GetService<INotesRepository>();
        var autosaveService = _services?.GetService<AutosaveService>();
        var themeService = _services?.GetService<ThemeService>();
        
        if (repository == null || autosaveService == null || themeService == null)
        {
            _logger?.LogWarning("Services not available for note reload");
            return;
        }

        var allNotes = await repository.GetAllActiveNotesAsync();
        var existingNoteIds = _openWindows.Select(w => (w.DataContext as NoteWindowViewModel)?.NoteId).ToHashSet();

        // Create windows for notes that don't have windows yet
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var note in allNotes)
            {
                if (!existingNoteIds.Contains(note.Id))
                {
                    CreateNoteWindow(note, autosaveService, themeService, repository);
                }
            }
        });
        
        _logger?.LogInformation("Notes reloaded, {Count} new windows created", 
            allNotes.Count - existingNoteIds.Count);
    }

    private void OnCreateNewNote()
    {
        var repository = _services?.GetService<INotesRepository>();
        var autosaveService = _services?.GetService<AutosaveService>();
        var themeService = _services?.GetService<ThemeService>();
        
        if (repository == null || autosaveService == null || themeService == null)
        {
            _logger?.LogWarning("Services not available for creating new note");
            return;
        }

        // Create new note with slight offset from last created note
        var lastNote = _openWindows.LastOrDefault();
        
        Task.Run(async () =>
        {
            var settings = await repository.GetSettingsAsync();
            var newNote = Note.CreateNew(settings.DefaultNoteColor);
            
            if (lastNote != null)
            {
                // Offset by 30 pixels to avoid overlap
                newNote.X = lastNote.Position.X + 30;
                newNote.Y = lastNote.Position.Y + 30;
            }

            await repository.UpsertNoteAsync(newNote);
            
            _logger?.LogInformation("Created new note {NoteId}", newNote.Id);
            
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CreateNoteWindow(newNote, autosaveService, themeService, repository);
            });
        });
    }

    private void OnCloseNoteWindow(NoteWindow window, Note note)
    {
        var repository = _services?.GetService<INotesRepository>();
        if (repository == null)
        {
            _logger?.LogWarning("Repository not available, cannot soft delete note");
            return;
        }

        // Soft delete the note
        Task.Run(async () =>
        {
            await repository.SoftDeleteNoteAsync(note.Id);
            _logger?.LogInformation("Note {NoteId} soft deleted", note.Id);
        });

        window.Close();
    }

    private void OnWindowClosed(NoteWindow window)
    {
        // Dispose viewmodel to clean up subscriptions
        if (window.DataContext is NoteWindowViewModel vm)
        {
            vm.Dispose();
        }
        
        _openWindows.Remove(window);
        
        _logger?.LogInformation("{Count} windows remaining", _openWindows.Count);

        // If all windows are closed, exit the application
        if (_openWindows.Count == 0)
        {
            _logger?.LogInformation("All windows closed, shutting down application");
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _logger?.LogInformation("Shutdown requested, flushing pending saves");
        
        // Flush all pending saves
        var autosaveService = _services?.GetService<AutosaveService>();
        if (autosaveService != null)
        {
            Task.Run(async () => await autosaveService.FlushAllAsync()).Wait();
            _logger?.LogInformation("All pending saves flushed");
        }

        // Dispose services (DI container will handle disposal)
        autosaveService?.Dispose();
        _services?.GetService<BackupService>()?.Dispose();
        _services?.GetService<NotesDbContext>()?.Dispose();
        
        
        if (_services is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }
        
        _logger?.LogInformation("Application shutdown complete");
        Serilog.Log.CloseAndFlush();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        _logger?.LogCritical(exception, "Unhandled exception occurred. IsTerminating: {IsTerminating}", 
            e.IsTerminating);
        
        // Try to show user-friendly error dialog if possible
        try
        {
            if (!e.IsTerminating)
            {
                _logger?.LogInformation("Attempting to continue after non-fatal unhandled exception");
            }
        }
        catch
        {
            // If we can't even log, just fail silently
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved(); // Prevent app crash
    }
}