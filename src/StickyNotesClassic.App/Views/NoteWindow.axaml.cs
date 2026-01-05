using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvRichTextBox;
using StickyNotesClassic.App.ViewModels;
using StickyNotesClassic.Core.Utilities;
using System;
using System.ComponentModel;
using System.Linq;

namespace StickyNotesClassic.App.Views;

public partial class NoteWindow : Window
{
    private NoteWindowViewModel? ViewModel => DataContext as NoteWindowViewModel;
    private Border? _headerBorder;
    private Canvas? _resizeGrip;
    private RichTextBox? _editor;
    private Button? _boldButton;
    private Button? _italicButton;
    private Button? _underlineButton;
    private bool _loadingDocument;
    private bool _formattingStatePending;

    public NoteWindow()
    {
        InitializeComponent();

        ApplyPlatformWindowSettings();

        // Header-only drag handle to avoid interfering with editor selection.
        _headerBorder = this.FindControl<Border>("HeaderBorder");
        if (_headerBorder != null)
        {
            _headerBorder.PointerPressed += OnPointerPressed;
        }

        // Get reference to resize grip and attach its handler
        _resizeGrip = this.FindControl<Canvas>("ResizeGrip");
        if (_resizeGrip != null)
        {
            _resizeGrip.PointerPressed += OnResizeGripPressed;
        }

        // Track position and size changes
        PositionChanged += OnPositionChanged;
        SizeChanged += OnSizeChanged;

        _editor = this.FindControl<RichTextBox>("RichEditor");
        _boldButton = this.FindControl<Button>("BoldButton");
        _italicButton = this.FindControl<Button>("ItalicButton");
        _underlineButton = this.FindControl<Button>("UnderlineButton");

        if (_editor != null)
        {
            _editor.PropertyChanged += OnEditorPropertyChanged;
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (ViewModel != null)
        {
            ViewModel.RequestOpenSettings += OnRequestOpenSettings;
            InitializeEditorContent();
        }
    }

    private void ApplyPlatformWindowSettings()
    {
        // macOS: prefer the native title bar and resizing chrome because
        // transparent, decoration-less windows lose resize hit-testing.
        if (OperatingSystem.IsMacOS())
        {
            SystemDecorations = SystemDecorations.Full;
            ExtendClientAreaToDecorationsHint = false;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            Background = Brushes.Transparent;
            return;
        }

        // Linux: several window managers suppress resize hit-testing when
        // client-area extensions remove the native border. Keep a minimal
        // system frame so resizing remains reliable while avoiding a full
        // title bar when possible.
        if (OperatingSystem.IsLinux())
        {
            SystemDecorations = SystemDecorations.BorderOnly;
            ExtendClientAreaToDecorationsHint = false;
            TransparencyLevelHint = new[]
            {
                WindowTransparencyLevel.Transparent,
                WindowTransparencyLevel.None
            };
            Background = Brushes.Transparent;
        }

        if (OperatingSystem.IsWindows())
        {
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 0;
        }
    }

    private void OnResizeGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginResizeDrag(WindowEdge.SouthEast, e);
            e.Handled = true; // Prevent event from bubbling to window
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only handle left button
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Visual visual && visual.FindAncestorOfType<Button>() != null)
        {
            return;
        }
        
        // Allow dragging the window
        BeginMoveDrag(e);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        if (ViewModel != null && Position.X >= 0 && Position.Y >= 0)
        {
            ViewModel.UpdateBounds(Position.X, Position.Y, Width, Height);
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (ViewModel != null && Position.X >= 0 && Position.Y >= 0)
        {
            ViewModel.UpdateBounds(Position.X, Position.Y, Width, Height);
        }
    }

    private async void OnRequestOpenSettings(object? sender, EventArgs e)
    {
        // Open settings window (will be wired via App later)
        // For now, trigger app-level settings via an event that App will handle
        if (Application.Current is App app)
        {
            await app.OpenSettingsWindowAsync();
        }
    }

    private void InitializeEditorContent()
    {
        if (_editor == null || ViewModel == null)
        {
            return;
        }

        _editor.KeyDown -= OnEditorKeyDown;
        _editor.KeyDown += OnEditorKeyDown;

        _loadingDocument = true;

        try
        {
            _editor.NewDocument();
            var normalized = RtfHelper.EnsureRtf(ViewModel.ContentRtf, ViewModel.ContentText, ViewModel.FontFamily, ViewModel.FontSize);
            _editor.LoadRtf(normalized);
            var flowDoc = _editor.FlowDocument;
            flowDoc.PagePadding = new Thickness(0);
            Dispatcher.UIThread.Post(() =>
            {
                flowDoc.Select(flowDoc.Selection.Start, 0);
            }, DispatcherPriority.Background);

            SyncEditorContent();

            if (_editor.FlowDocument != null)
            {
                _editor.FlowDocument.PropertyChanged -= OnFlowDocumentPropertyChanged;
                _editor.FlowDocument.PropertyChanged += OnFlowDocumentPropertyChanged;
            }

            ScheduleFormattingStateUpdate();
        }
        finally
        {
            _loadingDocument = false;
        }
    }

    private void OnFlowDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loadingDocument)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(FlowDocument.Text), StringComparison.Ordinal) || string.IsNullOrEmpty(e.PropertyName))
        {
            SyncEditorContent();
        }
    }

    private void SyncEditorContent()
    {
        if (_editor?.FlowDocument == null || ViewModel == null)
        {
            return;
        }

        var plainText = _editor.FlowDocument.Text ?? string.Empty;
        if (!string.Equals(ViewModel.ContentText, plainText, StringComparison.Ordinal))
        {
            ViewModel.ContentText = plainText;
        }

        var rtf = _editor.SaveRtf();
        if (!string.Equals(ViewModel.ContentRtf, rtf, StringComparison.Ordinal))
        {
            ViewModel.ContentRtf = rtf;
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        if ((e.KeyModifiers & modifier) == modifier)
        {
            switch (e.Key)
            {
                case Key.B:
                    ApplyFormatting(TextElement.FontWeightProperty, FontWeight.Bold, FontWeight.Normal);
                    e.Handled = true;
                    break;
                case Key.I:
                    ApplyFormatting(TextElement.FontStyleProperty, FontStyle.Italic, FontStyle.Normal);
                    e.Handled = true;
                    break;
                case Key.U:
                    ApplyFormatting(TextBlock.TextDecorationsProperty, TextDecorations.Underline, new TextDecorationCollection());
                    e.Handled = true;
                    break;
            }
        }
    }

    private void ApplyFormatting(AvaloniaProperty property, object onValue, object? offValue)
    {
        if (_editor?.FlowDocument?.Selection == null)
        {
            return;
        }

        var current = _editor.FlowDocument.Selection.GetFormatting(property);
        var target = Equals(current, onValue) ? offValue ?? new TextDecorationCollection() : onValue;
        _editor.FlowDocument.Selection.ApplyFormatting(property, target);
        SyncEditorContent();
        ScheduleFormattingStateUpdate();
    }

    private void OnBoldClicked(object? sender, RoutedEventArgs e)
    {
        ApplyFormatting(TextElement.FontWeightProperty, FontWeight.Bold, FontWeight.Normal);
    }

    private void OnItalicClicked(object? sender, RoutedEventArgs e)
    {
        ApplyFormatting(TextElement.FontStyleProperty, FontStyle.Italic, FontStyle.Normal);
    }

    private void OnUnderlineClicked(object? sender, RoutedEventArgs e)
    {
        ApplyFormatting(TextBlock.TextDecorationsProperty, TextDecorations.Underline, new TextDecorationCollection());
    }

    private void OnSelectAllClicked(object? sender, RoutedEventArgs e)
    {
        _editor?.FlowDocument?.SelectAll();
    }

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_loadingDocument)
        {
            return;
        }

        var propertyName = e.Property.Name;
        if (propertyName is "SelectionStart" or "SelectionEnd" or "SelectionLength" or "Selection")
        {
            ScheduleFormattingStateUpdate();
        }
    }

    private void ScheduleFormattingStateUpdate()
    {
        if (_formattingStatePending)
        {
            return;
        }

        _formattingStatePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _formattingStatePending = false;
            UpdateFormattingButtonStates();
        }, DispatcherPriority.Background);
    }

    private void UpdateFormattingButtonStates()
    {
        if (_editor?.FlowDocument?.Selection == null)
        {
            return;
        }

        UpdateButtonState(_boldButton, Equals(_editor.FlowDocument.Selection.GetFormatting(TextElement.FontWeightProperty), FontWeight.Bold));
        UpdateButtonState(_italicButton, Equals(_editor.FlowDocument.Selection.GetFormatting(TextElement.FontStyleProperty), FontStyle.Italic));
        UpdateButtonState(_underlineButton, IsUnderlineActive(_editor.FlowDocument.Selection.GetFormatting(TextBlock.TextDecorationsProperty)));
    }

    private static void UpdateButtonState(Button? button, bool isActive)
    {
        if (button == null)
        {
            return;
        }

        button.Classes.Set("is-active", isActive);
    }

    private static bool IsUnderlineActive(object? formattingValue)
    {
        if (formattingValue is TextDecorationCollection collection)
        {
            return collection.Any(decoration => decoration.Location == TextDecorationLocation.Underline);
        }

        return Equals(formattingValue, TextDecorations.Underline);
    }
}
