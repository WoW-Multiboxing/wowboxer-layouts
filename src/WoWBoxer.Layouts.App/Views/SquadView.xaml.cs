using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Threading;
using WoWBoxer.Layouts.App.ViewModels;

namespace WoWBoxer.Layouts.App.Views;

public partial class SquadView : UserControl
{
    public SquadView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is not MainViewModel vm) return;
            // Scroll after the list has processed the change (scrolling inside the event is re-entrant and throws).
            ((INotifyCollectionChanged)vm.Status.Lines).CollectionChanged += (_, _) =>
                Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
                {
                    if (StatusList.Items.Count > 0) StatusList.ScrollIntoView(StatusList.Items[^1]);
                });
        };
    }
}
