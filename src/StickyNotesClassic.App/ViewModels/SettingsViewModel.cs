using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using StickyNotesClassic.Core.Models;
using StickyNotesClassic.Core.Repositories;
using StickyNotesClassic.App.Messages;
using StickyNotesClassic.Core.Utilities;
using StickyNotesClassic.App.Services.Hotkeys;
using StickyNotesClassic.App.Services;
using StickyNotesClassic.Core.Services;

namespace StickyNotesClassic.App.ViewModels;

/// <summary>
/// ViewModel for Settings window.
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly INotesRepository _repository;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IHotkeyRegistrar _hotkeyRegistrar;
    private readonly IDialogService _dialogService;
    private readonly BackupService _backupService;
    private AppSettings _settings;
    private bool _isApplyingFontToAllNotes;
    private string _latestBackupStatus = "Loading backups...";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? RequestClose;
    public event EventHandler<string>? RequestExport;
    public event EventHandler<string>? RequestImport;
    public event EventHandler<string>? RequestRestore;

    public SettingsViewModel(INotesRepository repository, IHotkeyRegistrar hotkeyRegistrar, IDialogService dialogService, BackupService backupService, ILogger<SettingsViewModel> logger)
    {
        _repository = repository;
        _logger = logger;
        _hotkeyRegistrar = hotkeyRegistrar;
        _dialogService = dialogService;
        _backupService = backupService;
        _settings = new AppSettings(); // Will be loaded async

        SaveCommand = new RelayCommand(OnSave, CanSave);
        CancelCommand = new RelayCommand(OnCancel);
        ExportCommand = new RelayCommand(OnExport);
        ImportCommand = new RelayCommand(OnImport);
        ApplyFontToAllNotesCommand = new RelayCommand(OnApplyFontToAllNotes);
        RestoreLatestBackupCommand = new RelayCommand(OnRestoreLatestBackup);
        RestoreBackupCommand = new RelayCommand(OnRestoreBackup);

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
        OnPropertyChanged(nameof(AskBeforeClose));
        OnPropertyChanged(nameof(HotkeyModifiers));
        OnPropertyChanged(nameof(HotkeyKey));
        OnPropertyChanged(nameof(AutoBackupEnabled));
        OnPropertyChanged(nameof(AutoBackupRetentionDays));
        OnPropertyChanged(nameof(AutoBackupRetentionCount));
        OnPropertyChanged(nameof(SupportsGlobalHotkey));
        OnPropertyChanged(nameof(HotkeyPlatformNotice));
        OnPropertyChanged(nameof(BackupLocationHint));
        OnPropertyChanged(nameof(CanApplyFontToAllNotes));
        await RefreshLatestBackupStatusAsync();
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

    public int AutoBackupRetentionCount
    {
        get => _settings.AutoBackupRetentionCount;
        set
        {
            var clamped = Math.Max(1, Math.Min(365, value));
            if (_settings.AutoBackupRetentionCount != clamped)
            {
                _settings.AutoBackupRetentionCount = clamped;
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
            _logger.LogDebug("DefaultNoteColorIndex setter called with value: {Value}", value);
            var colorValue = (NoteColor)value;
            if (_settings.DefaultNoteColor != colorValue)
            {
                _logger.LogDebug("Setting DefaultNoteColor from {OldColor} to {NewColor}", _settings.DefaultNoteColor, colorValue);
                _settings.DefaultNoteColor = colorValue;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DefaultNoteColor));
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

    public bool AskBeforeClose
    {
        get => _settings.AskBeforeClose;
        set
        {
            if (_settings.AskBeforeClose != value)
            {
                _settings.AskBeforeClose = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsApplyingFontToAllNotes
    {
        get => _isApplyingFontToAllNotes;
        private set
        {
            if (_isApplyingFontToAllNotes != value)
            {
                _isApplyingFontToAllNotes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanApplyFontToAllNotes));
            }
        }
    }

    public bool CanApplyFontToAllNotes => !IsApplyingFontToAllNotes;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ApplyFontToAllNotesCommand { get; }
    public ICommand RestoreLatestBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }

    public bool SupportsGlobalHotkey => _hotkeyRegistrar.IsSupported;

    public string HotkeyPlatformNotice => SupportsGlobalHotkey
        ? "Global hotkeys are supported on this platform."
        : $"Global hotkeys are unavailable ({_hotkeyRegistrar.UnsupportedReason ?? "platform limitation"}).";

    public string BackupLocationHint => $"Backups saved to: {GetBackupDirectory()}";

    public string LatestBackupStatus
    {
        get => _latestBackupStatus;
        private set
        {
            if (_latestBackupStatus != value)
            {
                _latestBackupStatus = value;
                OnPropertyChanged();
            }
        }
    }

    private bool CanSave()
    {
        // Validate hotkey has at least one modifier and a key
        return !SupportsGlobalHotkey ||
               (!string.IsNullOrWhiteSpace(HotkeyKey) &&
                !string.IsNullOrWhiteSpace(HotkeyModifiers));
    }

    private async void OnSave()
    {
        _logger.LogInformation("Saving settings, DefaultNoteColor: {Color}", _settings.DefaultNoteColor);
        
        // Save settings to database
        await _repository.SaveSettingsAsync(_settings);
        
        _logger.LogInformation("Settings saved to database successfully");
        
        // Send message instead of static event
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
        
        _logger.LogDebug("SettingsChanged message sent");
        
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

    private async void OnRestoreLatestBackup()
    {
        try
        {
            var backups = await _backupService.ListBackupsAsync();
            var latest = backups.FirstOrDefault();

            if (latest == null)
            {
                await _dialogService.ShowMessageAsync("Restore Backup", "No backups were found in the backup directory.");
                return;
            }

            var status = latest.ChecksumPresent
                ? (latest.ChecksumValid ? "checksum validated" : "checksum mismatch")
                : "no checksum present";

            var localTimestamp = latest.LastWriteTimeUtc.ToLocalTime().ToString("g");
            var message = $"Restore backup '{Path.GetFileName(latest.FilePath)}' from {localTimestamp}?\nStatus: {status}.";

            var confirmation = await _dialogService.ShowConfirmationAsync(new ConfirmationDialogOptions
            {
                Title = "Restore Latest Backup",
                Message = message,
                ConfirmText = "Restore",
                CancelText = "Cancel",
                IsDestructive = true
            });

            if (!confirmation.Confirmed)
            {
                return;
            }

            RequestRestore?.Invoke(this, latest.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate restore");
            await _dialogService.ShowMessageAsync("Restore Failed", ex.Message);
        }
    }

    private async void OnRestoreBackup()
    {
        try
        {
            var backups = await _backupService.ListBackupsAsync();
            if (backups.Count == 0)
            {
                await _dialogService.ShowMessageAsync("Restore Backup", "No backups were found in the backup directory.");
                return;
            }

            var selected = await _dialogService.ShowBackupPickerAsync(backups);
            if (string.IsNullOrWhiteSpace(selected))
            {
                return;
            }

            RequestRestore?.Invoke(this, selected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup");
            await _dialogService.ShowMessageAsync("Restore Failed", ex.Message);
        }
    }

    private async void OnApplyFontToAllNotes()
    {
        if (IsApplyingFontToAllNotes)
        {
            return;
        }

        var validation = ValidationService.ValidateFontFamily(DefaultFontFamily);
        if (!validation.IsValid)
        {
            await _dialogService.ShowMessageAsync("Cannot Apply Font", validation.ErrorMessage ?? "Invalid font selection.");
            return;
        }

        IsApplyingFontToAllNotes = true;

        try
        {
            var updatedCount = await _repository.UpdateAllNoteFontsAsync(DefaultFontFamily, DefaultFontSize);
            await _dialogService.ShowMessageAsync("Font Applied",
                $"Updated {updatedCount} note(s) to use {DefaultFontFamily} {DefaultFontSize:0} pt.");

            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply font to all notes");
            await _dialogService.ShowMessageAsync("Font Update Failed", ex.Message);
        }
        finally
        {
            IsApplyingFontToAllNotes = false;
        }
    }

    private static string GetBackupDirectory() => AppPathHelper.GetBackupsDirectory();

    private async Task RefreshLatestBackupStatusAsync()
    {
        try
        {
            var backups = await _backupService.ListBackupsAsync();
            var latest = backups.FirstOrDefault();

            if (latest == null)
            {
                LatestBackupStatus = "No backups found.";
                return;
            }

            var status = latest.ChecksumPresent
                ? (latest.ChecksumValid ? "checksum validated" : "checksum mismatch")
                : "no checksum";
            LatestBackupStatus = $"Latest backup: {Path.GetFileName(latest.FilePath)} ({latest.LastWriteTimeUtc.ToLocalTime():g}, {status})";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate backups");
            LatestBackupStatus = "Unable to read backups.";
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        // Re-evaluate CanSave when relevant properties change
        if (propertyName == nameof(HotkeyKey) || propertyName == nameof(HotkeyModifiers))
        {
            (SaveCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
    }
}
