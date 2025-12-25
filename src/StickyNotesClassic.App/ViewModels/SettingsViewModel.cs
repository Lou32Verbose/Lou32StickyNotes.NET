using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Repositories;

namespace StickyNotesClassic.App.ViewModels;

/// <summary>
/// ViewModel for Settings window.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly INotesRepository _repository;
    private AppSettings _settings;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? RequestClose;
    public event EventHandler<string>? RequestExport;
    public event EventHandler<string>? RequestImport;

    public SettingsViewModel(INotesRepository repository)
    {
        _repository = repository;
        _settings = new AppSettings(); // Will be loaded async

        SaveCommand = new RelayCommand(OnSave, CanSave);
        CancelCommand = new RelayCommand(OnCancel);
        ExportCommand = new RelayCommand(OnExport);
        ImportCommand = new RelayCommand(OnImport);
        ApplyFontToAllNotesCommand = new RelayCommand(OnApplyFontToAllNotes);

        LoadSettingsAsync();
    }

    private async void LoadSettingsAsync()
    {
        _settings = await _repository.GetSettingsAsync();
        OnPropertyChanged(nameof(DefaultFontFamily));
        OnPropertyChanged(nameof(DefaultFontSize));
        OnPropertyChanged(nameof(DefaultNoteColor));
        OnPropertyChanged(nameof(DefaultNoteColorIndex));
        OnPropertyChanged(nameof(EnableBackgroundGradient));
        OnPropertyChanged(nameof(EnableEnhancedShadow));
        OnPropertyChanged(nameof(EnableGlossyHeader));
        OnPropertyChanged(nameof(EnableTextShadow));
        OnPropertyChanged(nameof(HotkeyModifiers));
        OnPropertyChanged(nameof(HotkeyKey));
        OnPropertyChanged(nameof(AutoBackupEnabled));
        OnPropertyChanged(nameof(AutoBackupRetentionDays));
    }

    public string DefaultFontFamily
    {
        get => _settings.DefaultFontFamily;
        set
        {
            if (_settings.DefaultFontFamily != value)
            {
                _settings.DefaultFontFamily = value;
                OnPropertyChanged();
            }
        }
    }

    public double DefaultFontSize
    {
        get => _settings.DefaultFontSize;
        set
        {
            var clamped = Math.Max(8, Math.Min(72, value));
            if (Math.Abs(_settings.DefaultFontSize - clamped) > 0.01)
            {
                _settings.DefaultFontSize = clamped;
                OnPropertyChanged();
            }
        }
    }

    public string HotkeyModifiers
    {
        get => _settings.HotkeyModifiers;
        set
        {
            if (_settings.HotkeyModifiers != value)
            {
                _settings.HotkeyModifiers = value;
                OnPropertyChanged();
            }
        }
    }

    public string HotkeyKey
    {
        get => _settings.HotkeyKey;
        set
        {
            if (_settings.HotkeyKey != value)
            {
                _settings.HotkeyKey = value;
                OnPropertyChanged();
            }
        }
    }

    public bool AutoBackupEnabled
    {
        get => _settings.AutoBackupEnabled;
        set
        {
            if (_settings.AutoBackupEnabled != value)
            {
                _settings.AutoBackupEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public int AutoBackupRetentionDays
    {
        get => _settings.AutoBackupRetentionDays;
        set
        {
            var clamped = Math.Max(1, Math.Min(365, value));
            if (_settings.AutoBackupRetentionDays != clamped)
            {
                _settings.AutoBackupRetentionDays = clamped;
                OnPropertyChanged();
            }
        }
    }

    public NoteColor DefaultNoteColor
    {
        get => _settings.DefaultNoteColor;
        set
        {
            if (_settings.DefaultNoteColor != value)
            {
                _settings.DefaultNoteColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DefaultNoteColorIndex));
            }
        }
    }

    public int DefaultNoteColorIndex
    {
        get => (int)_settings.DefaultNoteColor;
        set
        {
            Console.WriteLine($"DefaultNoteColorIndex setter called with value: {value}");
            var colorValue = (NoteColor)value;
            if (_settings.DefaultNoteColor != colorValue)
            {
                Console.WriteLine($"Setting DefaultNoteColor from {_settings.DefaultNoteColor} to {colorValue}");
                _settings.DefaultNoteColor = colorValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DefaultNoteColor));
            }
            else
            {
                Console.WriteLine($"Color unchanged, still {_settings.DefaultNoteColor}");
            }
        }
    }

    public bool EnableBackgroundGradient
    {
        get => _settings.EnableBackgroundGradient;
        set
        {
            if (_settings.EnableBackgroundGradient != value)
            {
                _settings.EnableBackgroundGradient = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableEnhancedShadow
    {
        get => _settings.EnableEnhancedShadow;
        set
        {
            if (_settings.EnableEnhancedShadow != value)
            {
                _settings.EnableEnhancedShadow = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableGlossyHeader
    {
        get => _settings.EnableGlossyHeader;
        set
        {
            if (_settings.EnableGlossyHeader != value)
            {
                _settings.EnableGlossyHeader = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableTextShadow
    {
        get => _settings.EnableTextShadow;
        set
        {
            if (_settings.EnableTextShadow != value)
            {
                _settings.EnableTextShadow = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ApplyFontToAllNotesCommand { get; }

    private bool CanSave()
    {
        // Validate hotkey has at least one modifier and a key
        return !string.IsNullOrWhiteSpace(HotkeyKey) && 
               !string.IsNullOrWhiteSpace(HotkeyModifiers);
    }

    private async void OnSave()
    {
        Console.WriteLine($"OnSave called, saving DefaultNoteColor: {_settings.DefaultNoteColor}");
        
        // Save settings to database
        await _repository.SaveSettingsAsync(_settings);
        
        Console.WriteLine("Settings saved to database");
        
        // Raise event AFTER save completes so handlers read updated values
        App.RaiseSettingsChanged();
        
        Console.WriteLine("SettingsChanged event raised");
        
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private void OnExport()
    {
        // Request will be handled by code-behind via file picker
        RequestExport?.Invoke(this, string.Empty);
    }

    private void OnImport()
    {
        // Request will be handled by code-behind via file picker
        RequestImport?.Invoke(this, string.Empty);
    }

    private async void OnApplyFontToAllNotes()
    {
        // TODO: This would update all notes' RTF to use new font
        // For now with plain text, this is a no-op
        // Future: Iterate notes and update RTF font tags
        await System.Threading.Tasks.Task.CompletedTask;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        // Re-evaluate CanSave when relevant properties change
        if (propertyName == nameof(HotkeyKey) || propertyName == nameof(HotkeyModifiers))
        {
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
}
