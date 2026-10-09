using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskRail.Core.Models;

namespace DeskRail.App.ViewModels;

public partial class PanelViewModel : ObservableObject
{
    [ObservableProperty]
    private double _panelWidth = 520;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private GlassMode _glassMode = GlassMode.Dark;

    public double PanelHeight => SystemParameters.PrimaryScreenHeight;

    public Action? OpenRequested;
    public Action? CloseRequested;
    public Action? CloseAnimationCompleted;

    public void RefreshScreenMetrics()
    {
        OnPropertyChanged(nameof(PanelHeight));
    }

    [RelayCommand]
    private void Open()
    {
        if (IsOpen)
            return;

        IsOpen = true;
        OpenRequested?.Invoke();
    }

    [RelayCommand]
    private void Close()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        CloseRequested?.Invoke();
    }
}
