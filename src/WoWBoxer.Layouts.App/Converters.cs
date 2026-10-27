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
