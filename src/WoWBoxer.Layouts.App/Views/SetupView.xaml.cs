using System.Windows;
using System.Windows.Controls;
using WoWBoxer.Layouts.App.ViewModels;

namespace WoWBoxer.Layouts.App.Views;

public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();
        Canvas.SlotEdited += (_, e) => (DataContext as MainViewModel)?.OnSlotEdited(e.Index, e.Slot);
        Canvas.SlotsSwapped += (_, e) => (DataContext as MainViewModel)?.SwapSlots(e.A, e.B);
    }

    /// <summary>"More ▾" opens its menu on a normal click.</summary>
    private void OnMore(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.DataContext = DataContext;
            menu.IsOpen = true;
        }
    }
}
