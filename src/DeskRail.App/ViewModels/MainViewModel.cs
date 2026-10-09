using CommunityToolkit.Mvvm.ComponentModel;

namespace DeskRail.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly PanelViewModel _panelVM;
    private readonly IconGridViewModel _iconGridVM;

    public MainViewModel(PanelViewModel panelVM, IconGridViewModel iconGridVM)
    {
        _panelVM = panelVM;
        _iconGridVM = iconGridVM;
    }

    public PanelViewModel PanelVM => _panelVM;
    public IconGridViewModel IconGridVM => _iconGridVM;
}
