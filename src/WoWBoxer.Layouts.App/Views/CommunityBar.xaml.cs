using System.Windows;
using System.Windows.Controls;

namespace WoWBoxer.Layouts.App.Views;

/// <summary>Discord, YouTube and website icon links (status bar: small icons; About: with labels).</summary>
public partial class CommunityBar : UserControl
{
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(CommunityBar), new PropertyMetadata(15.0));
    public static readonly DependencyProperty ShowLabelsProperty = DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(CommunityBar), new PropertyMetadata(false));

    public CommunityBar() => InitializeComponent();

    public double IconSize { get => (double)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public bool ShowLabels { get => (bool)GetValue(ShowLabelsProperty); set => SetValue(ShowLabelsProperty, value); }
}
