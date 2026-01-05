using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StickyNotesClassic.App.ViewModels;
using StickyNotesClassic.App.Views;

namespace StickyNotesClassic.App.Services;

/// <summary>
/// Avalonia-backed dialog service that applies platform-specific conventions.
/// </summary>
public class DialogService : IDialogService
{
    public async Task<DialogResult> ShowConfirmationAsync(ConfirmationDialogOptions options, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = TryGetActiveWindow();
            var completion = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var dialog = BuildDialogWindow(options.Title, owner);

            var messageBlock = new TextBlock
            {
                Text = options.Message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                MaxWidth = 420
            };

            var doNotAskAgainCheckbox = new CheckBox
            {
                Content = "Don't ask again",
                Margin = new Thickness(0, 8, 0, 0),
                IsVisible = options.ShowDoNotAskAgain
            };

            var confirmButton = new Button
            {
                Content = options.ConfirmText
            };

            if (options.IsDestructive)
            {
                confirmButton.Classes.Add("destructive");
            }

            var cancelButton = new Button
            {
                Content = options.CancelText
            };

            confirmButton.Click += (_, __) =>
            {
                completion.TrySetResult(new DialogResult(true, doNotAskAgainCheckbox.IsChecked == true));
                dialog.Close();
            };

            cancelButton.Click += (_, __) =>
            {
                completion.TrySetResult(new DialogResult(false, doNotAskAgainCheckbox.IsChecked == true));
                dialog.Close();
            };

            dialog.Closed += (_, __) =>
            {
                completion.TrySetResult(new DialogResult(false, doNotAskAgainCheckbox.IsChecked == true));
            };

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };

            // macOS typically places destructive actions to the left of cancel.
            if (OperatingSystem.IsMacOS() && options.IsDestructive)
            {
                buttonPanel.Children.Add(confirmButton);
                buttonPanel.Children.Add(cancelButton);
            }
            else
            {
                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(confirmButton);
            }

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Children =
                {
                    messageBlock,
                    doNotAskAgainCheckbox,
                    buttonPanel
                }
            };

            await ShowWindowAsync(dialog, owner, cancellationToken);
            return await completion.Task.WaitAsync(cancellationToken);
        });
    }

    public async Task ShowMessageAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = TryGetActiveWindow();
            var dialog = BuildDialogWindow(title, owner);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var okButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Right
            };

            okButton.Click += (_, __) =>
            {
                completion.TrySetResult();
                dialog.Close();
            };

            dialog.Closed += (_, __) => completion.TrySetResult();

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 420
                    },
                    okButton
                }
            };

            await ShowWindowAsync(dialog, owner, cancellationToken);
            await completion.Task.WaitAsync(cancellationToken);
        });
    }

    public async Task<string?> ShowBackupPickerAsync(IReadOnlyList<BackupSummary> backups, CancellationToken cancellationToken = default)
    {
        return await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = TryGetActiveWindow();
            var window = new RestoreBackupWindow
            {
                DataContext = new RestoreBackupViewModel(backups)
            };

            string? result;
            if (owner != null)
            {
                result = await window.ShowDialog<string?>(owner); // returns null if cancelled
            }
            else
            {
                var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                window.Closed += (_, __) => completion.TrySetResult(null);
                window.Show();
                result = await completion.Task.WaitAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }, DispatcherPriority.Background);
    }

    private static Window? TryGetActiveWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            return desktopLifetime.Windows.FirstOrDefault(w => w.IsActive) ?? desktopLifetime.Windows.FirstOrDefault();
        }

        return null;
    }

    private static Window BuildDialogWindow(string title, Window? owner)
    {
        var window = new Window
        {
            Title = title,
            Width = 480,
            MaxWidth = 520,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
        };

        if (owner != null)
        {
            window.Icon = owner.Icon;
        }

        return window;
    }

    private static async Task ShowWindowAsync(Window dialog, Window? owner, CancellationToken cancellationToken)
    {
        if (owner != null)
        {
            await dialog.ShowDialog(owner).WaitAsync(cancellationToken);
        }
        else
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnClosed(object? sender, EventArgs e)
            {
                completion.TrySetResult();
            }

            dialog.Closed += OnClosed;
            dialog.Show();

            try
            {
                await completion.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                dialog.Closed -= OnClosed;
            }
        }
    }
}
