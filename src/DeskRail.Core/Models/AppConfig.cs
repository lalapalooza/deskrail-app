namespace DeskRail.Core.Models;

public class AppConfig
{
    public TriggerConfig Trigger { get; set; } = new();
    public PanelConfig Panel { get; set; } = new();
    public GridConfig Grid { get; set; } = new();
    public SortConfig Sort { get; set; } = new();
    public TakeoverConfig Takeover { get; set; } = new();
    public GeneralConfig General { get; set; } = new();
}

public class TriggerConfig
{
    public string Color { get; set; } = "#00E5A0";
    public int VisibleWidth { get; set; } = 1;
    public int HitAreaWidth { get; set; } = 14;
    public double HeightRatio { get; set; } = 0.5;
    public bool BreathEnabled { get; set; } = true;
}

public class PanelConfig
{
    public double Width { get; set; } = 520;
    public double MinWidth { get; set; } = 320;
    public double MaxWidth { get; set; } = 800;
    public GlassMode GlassMode { get; set; } = GlassMode.Dark;
    public int AnimationDuration { get; set; } = 450;
}

public class GridConfig
{
    public double ColGap { get; set; } = 16;
    public double RowGap { get; set; } = 12;
    public double IconSize { get; set; } = 72;
    public int LabelMaxLines { get; set; } = 2;
    public ViewMode ViewMode { get; set; } = ViewMode.Grid;
}

public class SortConfig
{
    public SortKey SortKey { get; set; } = SortKey.Name;
    public bool Ascending { get; set; } = true;
}

public class TakeoverConfig
{
    public bool Enabled { get; set; } = false;
    public bool AutoStart { get; set; } = false;
}

public class GeneralConfig
{
    public bool ShowHiddenFiles { get; set; } = false;
    public string Language { get; set; } = "zh-CN";
}

public enum GlassMode { Dark, Light }
public enum ViewMode { Grid, List }
public enum SortKey { Name, Type, Size, Date }
