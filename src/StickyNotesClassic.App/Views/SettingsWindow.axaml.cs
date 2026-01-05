using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using StickyNotesClassic.App.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StickyNotesClassic.App.Views;

public partial class SettingsWindow : Window
{
    private SettingsViewModel? ViewModel => DataContext as SettingsViewModel;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (ViewModel != null)
        {
            ViewModel.RequestClose += OnRequestClose;
            ViewModel.RequestExport += OnRequestExport;
            ViewModel.RequestImport += OnRequestImport;
            ViewModel.RequestRestore += OnRequestRestore;
        }
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        Close();
    }

    private async void OnRequestExport(object? sender, string e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Notes",
            DefaultExtension = "json",
            SuggestedFileName = $"sticky_notes_{DateTime.Now:yyyy-MM-dd}.json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (file != null)
        {
            try
            {
                // Get BackupService from App
                if (Application.Current is App app && app.BackupService != null)
                {
                    var result = await app.BackupService.ExportNotesAsync(file.Path.LocalPath);

                    // Show success message
                    await ShowMessageAsync("Export Successful",
                        $"Exported {result.ExportedCount} note(s) to:\n{result.FilePath}");
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Export Failed", $"Error: {ex.Message}");
            }
        }
    }

    private async void OnRequestImport(object? sender, string e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Notes",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });

        if (files.Any())
        {
            var file = files.First();
            
            try
            {
                // Get BackupService from App
                if (Application.Current is App app && app.BackupService != null)
                {
                    var result = await app.BackupService.ImportNotesAsync(file.Path.LocalPath);

                    // Reload notes to show imported ones
                    await app.ReloadNotesAsync();

                    // Show summary with any skips
                    var message = new StringBuilder();
                    message.AppendLine($"Imported {result.ImportedCount} note(s) from:");
                    message.AppendLine(file.Path.LocalPath);

                    if (result.SkippedCount > 0)
                    {
                        message.AppendLine();
                        message.AppendLine($"Skipped {result.SkippedCount} item(s) due to errors.");

                        foreach (var failure in result.Failures.Take(3))
                        {
                            message.AppendLine($"- {failure.Reason}");
                        }

                        if (result.Failures.Count > 3)
                        {
                            message.AppendLine($"...and {result.Failures.Count - 3} more");
                        }

                        message.AppendLine();
                        message.AppendLine($"Check logs at: {result.LogDirectory}");
                    }

                    // Show success message
                    await ShowMessageAsync("Import Completed", message.ToString());
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Import Failed", $"Error: {ex.Message}");
            }
        }
    }

    private async void OnRequestRestore(object? sender, string filePath)
    {
        try
        {
            if (Application.Current is App app && app.BackupService != null)
            {
                var result = await app.BackupService.ImportNotesAsync(filePath);
                await app.ReloadNotesAsync();

                var builder = new StringBuilder();
                builder.AppendLine($"Restored {result.ImportedCount} note(s) from:");
                builder.AppendLine(filePath);

                if (result.SkippedCount > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine($"Skipped {result.SkippedCount} item(s):");
                    foreach (var failure in result.Failures.Take(3))
                    {
                        builder.AppendLine($"- {failure.Reason}");
                    }

                    if (result.Failures.Count > 3)
                    {
                        builder.AppendLine($"...and {result.Failures.Count - 3} more");
                    }
                }

                await ShowMessageAsync("Restore Completed", builder.ToString());
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Restore Failed", $"Error: {ex.Message}");
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var msgBox = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new Button
                    {
                        Name = "OKButton",
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
                    }
                }
            }
        };

        var okButton = msgBox.FindControl<Button>("OKButton");
        if (okButton != null)
        {
            okButton.Click += (s, e) => msgBox.Close();
        }
        
        await msgBox.ShowDialog(this);
    }

    // Simple RelayCommand for button
    private class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        
#pragma warning disable CS0067 // Event is never used (stub for ICommand)
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
        
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}
