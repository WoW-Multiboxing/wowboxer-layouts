using System.Globalization;
using System.Windows.Data;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.App;

public sealed class PresetNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is PresetKind kind ? Presets.DisplayName(kind) : value?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class InvertBool : IValueConverter
{
    public static readonly InvertBool Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b ? !b : value;
}

public sealed class HalfConverter : IValueConverter
{
    public static readonly HalfConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is double d ? Math.Max(0, d / 2 - 2) : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Visible when the value is true (or, with parameter "not", when false).</summary>
public sealed class BoolVisibility : IValueConverter
{
    public static readonly BoolVisibility Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var visible = value is true || (value is int i && i > 0) || (value is string s && s.Length > 0);
        if (parameter as string == "not") visible = !visible;
        return visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>True when the value equals the parameter (radio buttons bound to an enum or a number).</summary>
public sealed class EqualsConverter : IValueConverter
{
    public static readonly EqualsConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return Binding.DoNothing;
        if (targetType.IsEnum) return Enum.Parse(targetType, parameter.ToString()!);
        if (targetType == typeof(int)) return int.Parse(parameter.ToString()!, CultureInfo.InvariantCulture);
        return parameter;
    }
}

/// <summary>Visible when the selected tab equals the parameter.</summary>
public sealed class TabVisibility : IValueConverter
{
    public static readonly TabVisibility Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Brush for a squad member's state chip.</summary>
public sealed class PartyStateBrush : IValueConverter
{
    public static readonly PartyStateBrush Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        ViewModels.PartyState.Connected => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8C, 0xCB, 0x7A)),
        ViewModels.PartyState.Moved or ViewModels.PartyState.Placing => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE6, 0xB4, 0x55)),
        _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA7, 0x9F, 0x8A)),
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Shows the parameter text instead of an empty string.</summary>
public sealed class EmptyAs : IValueConverter
{
    public static readonly EmptyAs Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } s ? s : parameter?.ToString() ?? "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
