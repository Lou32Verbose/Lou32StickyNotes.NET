using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using StickyNotesClassic.App.Services;

namespace StickyNotesClassic.App.ViewModels;

public class RestoreBackupViewModel
{
    public ObservableCollection<BackupEntry> Backups { get; } = new();

    public BackupEntry? SelectedBackup { get; set; }

    public ICommand ConfirmCommand { get; }

    public ICommand CancelCommand { get; }

    public event EventHandler<string?>? RequestClose;

    public RestoreBackupViewModel(IEnumerable<BackupSummary> summaries)
    {
        foreach (var summary in summaries.OrderByDescending(s => s.LastWriteTimeUtc))
        {
            Backups.Add(new BackupEntry(summary));
        }

        SelectedBackup = Backups.FirstOrDefault();

        ConfirmCommand = new RelayCommand(OnConfirm, CanConfirm);
        CancelCommand = new RelayCommand(OnCancel);
    }

    private bool CanConfirm() => SelectedBackup != null;

    private void OnConfirm()
    {
        if (!CanConfirm())
        {
            return;
        }

        RequestClose?.Invoke(this, SelectedBackup!.FilePath);
    }

    private void OnCancel()
    {
        RequestClose?.Invoke(this, null);
    }
}

public sealed record BackupEntry
{
    public BackupEntry(BackupSummary summary)
    {
        FilePath = summary.FilePath;
        FileName = System.IO.Path.GetFileName(summary.FilePath);
        TimestampLocal = summary.LastWriteTimeUtc.ToLocalTime();
        NoteCount = summary.NoteCount;
        Status = summary.ChecksumPresent
            ? (summary.ChecksumValid ? "checksum ok" : "checksum mismatch")
            : "no checksum";
        Size = summary.SizeBytes;
    }

    public string FilePath { get; }

    public string FileName { get; }

    public DateTime TimestampLocal { get; }

    public int NoteCount { get; }

    public string Status { get; }

    public long Size { get; }
}
