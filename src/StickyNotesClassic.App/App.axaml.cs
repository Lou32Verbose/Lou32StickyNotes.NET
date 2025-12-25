using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
    private NotesDbContext? _dbContext;
    private INotesRepository? _repository;
    private AutosaveService? _autosaveService;
    private ThemeService? _themeService;
    private BackupService? _backupService;
    private readonly List<NoteWindow> _openWindows = new();

    /// <summary>
    /// Gets the BackupService instance for use by SettingsWindow.
    /// </summary>
    public BackupService? BackupService => _backupService;

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
        Console.WriteLine("=== App OnFrameworkInitializationCompleted called ===");
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Console.WriteLine("Initializing services...");
            
            // Initialize services
            _dbContext = new NotesDbContext();
            _repository = new NotesRepository(_dbContext);
            _autosaveService = new AutosaveService(_repository);
            _themeService = new ThemeService();
            _backupService = new BackupService(_repository);

            Console.WriteLine("Services initialized. Starting database initialization...");

            // Initialize database
            Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("Initializing database...");
                    await _dbContext.InitializeAsync();
                    Console.WriteLine("Database initialized.");
                    
                    // Load settings and schedule auto-backup
                    Console.WriteLine("Loading settings...");
                    var settings = await _repository.GetSettingsAsync();
                    Console.WriteLine($"Settings loaded. AutoBackup: {settings.AutoBackupEnabled}");
                    
                    if (settings.AutoBackupEnabled && _backupService != null)
                    {
                        _backupService.ScheduleDailyBackup(settings.AutoBackupRetentionDays);
                        Console.WriteLine("Auto-backup scheduled.");
                    }
                    
                    Console.WriteLine("Loading and showing notes...");
                    await LoadAndShowNotesAsync();
                    Console.WriteLine("Notes loaded and windows created.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR during initialization: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
            });

            // Handle shutdown
            desktop.ShutdownRequested += OnShutdownRequested;
            
            // Subscribe to settings changes to update note colors
            SettingsChanged += OnAppSettingsChanged;
        }
        else
        {
            Console.WriteLine("Not a desktop application lifetime!");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnAppSettingsChanged(object? sender, EventArgs e)
    {
        Console.WriteLine("=== OnAppSettingsChanged triggered ===");
        
        if (_repository == null)
        {
            Console.WriteLine("Repository is null, returning");
            return;
        }
            
        // Get the new default color
        var settings = await _repository.GetSettingsAsync();
        var newDefaultColor = settings.DefaultNoteColor;
        
        Console.WriteLine($"New default color: {newDefaultColor}");
        Console.WriteLine($"Number of open windows: {_openWindows.Count}");
        
        // Update all open note windows to use the new color
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var window in _openWindows)
            {
                if (window.DataContext is NoteWindowViewModel vm)
                {
                    Console.WriteLine($"Updating note {vm.NoteId} from {vm.Color} to {newDefaultColor}");
                    vm.Color = newDefaultColor;
                }
            }
        });
        
        Console.WriteLine("=== Color update complete ===");
    }

    private async Task LoadAndShowNotesAsync()
    {
        Console.WriteLine("LoadAndShowNotesAsync called");
        
        if (_repository == null || _autosaveService == null || _themeService == null)
        {
            Console.WriteLine("ERROR: Services are null!");
            return;
        }

        Console.WriteLine("Fetching all active notes from database...");
        var notes = await _repository.GetAllActiveNotesAsync();
        Console.WriteLine($"Found {notes.Count} active notes.");

        // If no notes exist, create a default yellow note
        if (notes.Count == 0)
        {
            Console.WriteLine("No notes found. Creating default note...");
            var settings = await _repository.GetSettingsAsync();
            var defaultNote = Note.CreateNew(settings.DefaultNoteColor);
            await _repository.UpsertNoteAsync(defaultNote);
            notes.Add(defaultNote);
            Console.WriteLine("Default note created.");
        }

        // Create a window for each note on UI thread
        Console.WriteLine("Creating windows on UI thread...");
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var note in notes)
            {
                Console.WriteLine($"Creating window for note {note.Id}...");
                CreateNoteWindow(note);
            }
            Console.WriteLine("All windows created.");
        });
    }

    private void CreateNoteWindow(Note note)
    {
        if (_autosaveService == null || _themeService == null || _repository == null)
            return;

        var viewModel = new NoteWindowViewModel(note, _autosaveService, _themeService, _repository);
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
        if (_repository == null) return;

        var settingsVm = new SettingsViewModel(_repository);
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
        if (_repository == null || _autosaveService == null || _themeService == null)
            return;

        var allNotes = await _repository.GetAllActiveNotesAsync();
        var existingNoteIds = _openWindows.Select(w => (w.DataContext as NoteWindowViewModel)?.NoteId).ToHashSet();

        // Create windows for notes that don't have windows yet
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var note in allNotes)
            {
                if (!existingNoteIds.Contains(note.Id))
                {
                    CreateNoteWindow(note);
                }
            }
        });
    }

    private void OnCreateNewNote()
    {
        if (_repository == null)
            return;

        // Create new note with slight offset from last created note
        var lastNote = _openWindows.LastOrDefault();
        
        Task.Run(async () =>
        {
            var settings = await _repository.GetSettingsAsync();
            var newNote = Note.CreateNew(settings.DefaultNoteColor);
            
            if (lastNote != null)
            {
                // Offset by 30 pixels to avoid overlap
                newNote.X = lastNote.Position.X + 30;
                newNote.Y = lastNote.Position.Y + 30;
            }

            await _repository.UpsertNoteAsync(newNote);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CreateNoteWindow(newNote);
            });
        });
    }

    private void OnCloseNoteWindow(NoteWindow window, Note note)
    {
        if (_repository == null)
            return;

        // Soft delete the note
        Task.Run(async () =>
        {
            await _repository.SoftDeleteNoteAsync(note.Id);
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

        // If all windows are closed, exit the application
        if (_openWindows.Count == 0)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Flush all pending saves
        if (_autosaveService != null)
        {
            Task.Run(async () => await _autosaveService.FlushAllAsync()).Wait();
        }

        // Dispose services
        _autosaveService?.Dispose();
        _backupService?.Dispose();
        _dbContext?.Dispose();
    }
}