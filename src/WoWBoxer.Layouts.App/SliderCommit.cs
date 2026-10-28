using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace WoWBoxer.Layouts.App;

/// <summary>
/// <c>app:SliderCommit.Command</c>: runs a command when a slider is let go (mouse up after a drag or click, or a key),
/// so a setting is written once on release instead of on every step of the drag. A value set without the mouse
/// (screen readers, UI Automation) is committed once it has rested for a moment.
/// </summary>
public static class SliderCommit
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(SliderCommit), new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject d) => (ICommand?)d.GetValue(CommandProperty);
    public static void SetCommand(DependencyObject d, ICommand? value) => d.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Slider slider || e.OldValue is not null) return;
        // handledEventsToo: the thumb and the track mark the mouse-up as handled.
        slider.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler((_, _) => Run(slider)), handledEventsToo: true);
        slider.KeyUp += (_, _) => Run(slider);
        var rest = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        rest.Tick += (_, _) =>
        {
            rest.Stop();
            Run(slider);
        };
        slider.ValueChanged += (_, _) =>
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;   // a drag commits on mouse up
            rest.Stop();
            rest.Start();
        };
    }

    private static void Run(Slider slider)
    {
        if (GetCommand(slider) is { } command && command.CanExecute(null)) command.Execute(null);
    }
}
