using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.App.ViewModels;

/// <summary>A monitor the Offhand companion can be on ("Right · 1920×1080").</summary>
public sealed record CompanionChoice(MonitorInfo Monitor, string Text)
{
    public override string ToString() => Text;
}
