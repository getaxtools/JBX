using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Jbx.Desktop.Text;
using Jbx.Platform;

namespace Jbx.Desktop.Controls;

/// <summary>A reachability shown as an icon and a word, never as colour alone.</summary>
internal sealed class StatusBadge : TemplatedControl
{
    public static readonly StyledProperty<Reachability> ReachabilityProperty =
        AvaloniaProperty.Register<StatusBadge, Reachability>(nameof(Reachability), Reachability.Unknown);

    public static readonly StyledProperty<bool> ShowLabelProperty =
        AvaloniaProperty.Register<StatusBadge, bool>(nameof(ShowLabel), true);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatusBadge, string>(nameof(Label), StatusText.Label(Reachability.Unknown));

    static StatusBadge()
    {
        ReachabilityProperty.Changed.AddClassHandler<StatusBadge>((badge, _) => badge.UpdateState());
    }

    public StatusBadge()
    {
        UpdateState();
    }

    public Reachability Reachability
    {
        get => GetValue(ReachabilityProperty);
        set => SetValue(ReachabilityProperty, value);
    }

    public bool ShowLabel
    {
        get => GetValue(ShowLabelProperty);
        set => SetValue(ShowLabelProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        private set => SetValue(LabelProperty, value);
    }

    private void UpdateState()
    {
        var reachability = Reachability;
        PseudoClasses.Set(":reachable", reachability == Reachability.Reachable);
        PseudoClasses.Set(":notfound", reachability == Reachability.NotFound);
        PseudoClasses.Set(":denied", reachability == Reachability.Denied);
        PseudoClasses.Set(":timeout", reachability == Reachability.Timeout);
        PseudoClasses.Set(":checking", reachability == Reachability.Checking);
        PseudoClasses.Set(":unknown", reachability == Reachability.Unknown);
        Label = StatusText.Label(reachability);
        AutomationProperties.SetName(this, Label);
    }
}
