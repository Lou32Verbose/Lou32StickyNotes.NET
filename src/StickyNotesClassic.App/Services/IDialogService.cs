using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StickyNotesClassic.App.Services;

/// <summary>
/// Provides UI dialogs for confirmations and notifications.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows a confirmation dialog with optional suppression.
    /// </summary>
    Task<DialogResult> ShowConfirmationAsync(ConfirmationDialogOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows an informational dialog with a single acknowledgement button.
    /// </summary>
    Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows a picker for available backups and returns the selected file path, or null if cancelled.
    /// </summary>
    Task<string?> ShowBackupPickerAsync(IReadOnlyList<BackupSummary> backups, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for configuring confirmation dialogs.
/// </summary>
public record ConfirmationDialogOptions
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string ConfirmText { get; init; } = "OK";
    public string CancelText { get; init; } = "Cancel";
    public bool IsDestructive { get; init; }
    public bool ShowDoNotAskAgain { get; init; }
};

/// <summary>
/// Result of a confirmation dialog.
/// </summary>
/// <param name="Confirmed">Whether the user confirmed the action.</param>
/// <param name="DoNotAskAgain">Whether the user requested to suppress future prompts.</param>
public record DialogResult(bool Confirmed, bool DoNotAskAgain);
