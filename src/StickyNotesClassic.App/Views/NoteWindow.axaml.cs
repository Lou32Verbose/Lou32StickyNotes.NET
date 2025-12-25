using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using StickyNotesClassic.App.ViewModels;
using System;

namespace StickyNotesClassic.App.Views;

public partial class NoteWindow : Window
{
    private NoteWindowViewModel? ViewModel => DataContext as NoteWindowViewModel;
    private Canvas? _resizeGrip;

    public NoteWindow()
    {
        InitializeComponent();
        
        // Get reference to resize grip and attach its handler
        _resizeGrip = this.FindControl<Canvas>("ResizeGrip");
        if (_resizeGrip != null)
        {
            _resizeGrip.PointerPressed += OnResizeGripPressed;
        }
        
        // Enable window dragging from anywhere else
        PointerPressed += OnPointerPressed;
        
        // Track position and size changes
        PositionChanged += OnPositionChanged;
        SizeChanged += OnSizeChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (ViewModel != null)
        {
            ViewModel.RequestOpenSettings += OnRequestOpenSettings;
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
}
