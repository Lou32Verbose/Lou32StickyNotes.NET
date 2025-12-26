using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Services;
using StickyNotesClassic.Core.Repositories;
using StickyNotesClassic.App.Services;
using StickyNotesClassic.App.Messages;

namespace StickyNotesClassic.App.ViewModels;

/// <summary>
/// ViewModel for a single note window.
/// </summary>
public class NoteWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AutosaveService _autosaveService;
    private readonly ThemeService _themeService;
    private readonly INotesRepository _repository;
    private readonly ILogger<NoteWindowViewModel> _logger;
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

    public NoteWindowViewModel(Note note, AutosaveService autosaveService, ThemeService themeService, INotesRepository repository, ILogger<NoteWindowViewModel> logger)
    {
        _note = note;
        _autosaveService = autosaveService;
        _themeService = themeService;
        _repository = repository;
        _logger = logger;
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
        
        // Subscribe to settings changes via messaging
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) => OnSettingsChangedMessage());
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        // Reload font settings when settings change
        LoadFontSettingsAsync();
    }

    private async void LoadFontSettingsAsync()
    {
        try
        {
            var settings = await _repository.GetSettingsAsync();
            FontFamily = settings.DefaultFontFamily;
            FontSize = settings.DefaultFontSize;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load font settings for note {NoteId}", _note.Id);
        }
    }

    private async void OnSettingsChangedMessage()
    {
        _logger.LogDebug("Received settings changed message for note {NoteId}", _note.Id);
        
        try
        {
            var settings = await _repository.GetSettingsAsync();
            FontFamily = settings.DefaultFontFamily;
            FontSize = settings.DefaultFontSize;
            
            EnableBackgroundGradient = settings.EnableBackgroundGradient;
            EnableEnhancedShadow = settings.EnableEnhancedShadow;
            EnableGlossyHeader = settings.EnableGlossyHeader;
            EnableTextShadow = settings.EnableTextShadow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply settings changes for note {NoteId}", _note.Id);
        }
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
                // Validate RTF content before accepting
                var validation = ValidationService.ValidateRtfContent(value);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Invalid RTF content rejected for note {NoteId}: {Error}", _note.Id, validation.ErrorMessage);
                    return;
                }
                
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
        // Unregister from messaging (automatic with weak references, but explicit is clearer)
        WeakReferenceMessenger.Default.Unregister<SettingsChangedMessage>(this);
    }
}
