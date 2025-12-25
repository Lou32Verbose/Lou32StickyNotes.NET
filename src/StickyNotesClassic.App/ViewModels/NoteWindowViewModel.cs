using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Services;
using StickyNotesClassic.Core.Repositories;
using StickyNotesClassic.App.Services;

namespace StickyNotesClassic.App.ViewModels;

/// <summary>
/// ViewModel for a single note window.
/// </summary>
public class NoteWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AutosaveService _autosaveService;
    private readonly ThemeService _themeService;
    private readonly INotesRepository _repository;
    private Note _note;
    private string _contentRtf;
    private string _contentText;
    private string _fontFamily = "Arial";
    private double _fontSize = 12.0;
    private bool _enableBackgroundGradient = true;
    private bool _enableEnhancedShadow = true;
    private bool _enableGlossyHeader = true;
    private bool _enableTextShadow = false;

    public event PropertyChangedEventHandler? PropertyChanged;

    public NoteWindowViewModel(Note note, AutosaveService autosaveService, ThemeService themeService, INotesRepository repository)
    {
        _note = note;
        _autosaveService = autosaveService;
        _themeService = themeService;
        _repository = repository;
        _contentRtf = note.ContentRtf;
        _contentText = note.ContentText;

        // Commands
        CreateNewNoteCommand = new RelayCommand(OnCreateNewNote);
        CloseNoteCommand = new RelayCommand(OnCloseNote);
        ToggleTopmostCommand = new RelayCommand(OnToggleTopmost);
        ChangeColorCommand = new RelayCommand<object>(p => OnChangeColor(p as NoteColor?));
        OpenSettingsCommand = new RelayCommand(OnOpenSettings);

        // Load font settings asynchronously
        LoadFontSettingsAsync();
        
        // Subscribe to settings changes
        App.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Reload font settings when settings change
        LoadFontSettingsAsync();
    }

    private async void LoadFontSettingsAsync()
    {
        var settings = await _repository.GetSettingsAsync();
        FontFamily = settings.DefaultFontFamily;
        FontSize = settings.DefaultFontSize;
        
        // Load visual effects settings
        Console.WriteLine($"[NoteWindowViewModel] Loading visual effects for note {_note.Id}:");
        Console.WriteLine($"  EnableBackgroundGradient: {settings.EnableBackgroundGradient}");
        Console.WriteLine($"  EnableEnhancedShadow: {settings.EnableEnhancedShadow}");
        Console.WriteLine($"  EnableGlossyHeader: {settings.EnableGlossyHeader}");
        Console.WriteLine($"  EnableTextShadow: {settings.EnableTextShadow}");
        
        EnableBackgroundGradient = settings.EnableBackgroundGradient;
        EnableEnhancedShadow = settings.EnableEnhancedShadow;
        EnableGlossyHeader = settings.EnableGlossyHeader;
        EnableTextShadow = settings.EnableTextShadow;
    }

    public Note Note => _note;

    public string NoteId => _note.Id;

    public NoteColor Color
    {
        get => _note.Color;
        set
        {
            if (_note.Color != value)
            {
                _note.Color = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentTheme));
                _autosaveService.EnqueueImmediateSave(_note);
            }
        }
    }

    public bool IsTopmost
    {
        get => _note.IsTopmost;
        set
        {
            if (_note.IsTopmost != value)
            {
                _note.IsTopmost = value;
                OnPropertyChanged();
                _autosaveService.EnqueueImmediateSave(_note);
            }
        }
    }

    public string ContentRtf
    {
        get => _contentRtf;
        set
        {
            if (_contentRtf != value)
            {
                _contentRtf = value;
                OnPropertyChanged();
                // Debounced save
                _autosaveService.EnqueueContentChanged(_note.Id, _contentRtf, _contentText);
            }
        }
    }

    public string ContentText
    {
        get => _contentText;
        set
        {
            if (_contentText != value)
            {
                _contentText = value;
                _note.ContentText = value;
                OnPropertyChanged();
            }
        }
    }

    public ThemeBrushes CurrentTheme => _themeService.GetTheme(_note.Color);

    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            if (_fontFamily != value)
            {
                _fontFamily = value;
                OnPropertyChanged();
            }
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (Math.Abs(_fontSize - value) > 0.01)
            {
                _fontSize = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableBackgroundGradient
    {
        get => _enableBackgroundGradient;
        set
        {
            if (_enableBackgroundGradient != value)
            {
                _enableBackgroundGradient = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableEnhancedShadow
    {
        get => _enableEnhancedShadow;
        set
        {
            if (_enableEnhancedShadow != value)
            {
                _enableEnhancedShadow = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableGlossyHeader
    {
        get => _enableGlossyHeader;
        set
        {
            if (_enableGlossyHeader != value)
            {
                _enableGlossyHeader = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableTextShadow
    {
        get => _enableTextShadow;
        set
        {
            if (_enableTextShadow != value)
            {
                _enableTextShadow = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand CreateNewNoteCommand { get; }
    public ICommand CloseNoteCommand { get; }
    public ICommand ToggleTopmostCommand { get; }
    public ICommand ChangeColorCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public event EventHandler? RequestCreateNewNote;
    public event EventHandler? RequestClose;
    public event EventHandler? RequestOpenSettings;

    public void UpdateBounds(double x, double y, double width, double height)
    {
        _note.X = x;
        _note.Y = y;
        _note.Width = width;
        _note.Height = height;

        // Throttled save
        _autosaveService.EnqueueBoundsChanged(_note.Id, x, y, width, height);
    }

    private void OnCreateNewNote()
    {
        RequestCreateNewNote?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseNote()
    {
        // Show confirmation if note has content
        if (!string.IsNullOrWhiteSpace(_contentText))
        {
            // TODO: Show confirmation dialog
            // For now, just close
        }

        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void OnToggleTopmost()
    {
        IsTopmost = !IsTopmost;
    }

    private void OnChangeColor(NoteColor? colorParam)
    {
        // Handle both NoteColor and integer (from MenuItem CommandParameter)
        NoteColor color;
        
        if (colorParam.HasValue)
        {
            color = colorParam.Value;
        }
        else
        {
            return; // No valid color
        }

        Color = color;
    }

    private void OnOpenSettings()
    {
        RequestOpenSettings?.Invoke(this, EventArgs.Empty);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        // Unsubscribe from settings changes to prevent memory leaks
        App.SettingsChanged -= OnSettingsChanged;
    }
}

/// <summary>
/// Simple relay command implementation.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Generic relay command implementation.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke((T?)parameter) ?? true;
    }

    public void Execute(object? parameter)
    {
        _execute((T?)parameter);
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
