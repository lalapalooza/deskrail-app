using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DeskRail.App.ViewModels;
using DeskRail.Core.Interop;

namespace DeskRail.App.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        UpdateScreenBounds();
    }

    public void UpdateScreenBounds()
    {
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        Left = 0;
        Top = 0;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);
    }

    private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PanelViewModel vm)
            vm.CloseCommand.Execute(null);
    }
}
