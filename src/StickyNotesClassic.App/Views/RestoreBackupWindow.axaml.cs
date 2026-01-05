using System;
using Avalonia.Controls;
using StickyNotesClassic.App.ViewModels;

namespace StickyNotesClassic.App.Views;

public partial class RestoreBackupWindow : Window
{
    public RestoreBackupWindow()
    {
        InitializeComponent();

        if (DataContext is RestoreBackupViewModel vm)
        {
            HookViewModel(vm);
        }
    }

    private void HookViewModel(RestoreBackupViewModel vm)
    {
        vm.RequestClose += (_, result) =>
        {
            Close(result);
        };
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is RestoreBackupViewModel vm)
        {
            HookViewModel(vm);
        }
    }
}
