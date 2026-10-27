using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Threading;
using WoWBoxer.Layouts.App.ViewModels;

namespace WoWBoxer.Layouts.App.Views;

public partial class SquadView : UserControl
{
    // While the tab is open, statuses and the checklist follow what happens (windows opening, Battle.net starting).
    private readonly DispatcherTimer _live = new() { Interval = TimeSpan.FromSeconds(2) };

    public SquadView()
    {
        InitializeComponent();
        _live.Tick += (_, _) => (DataContext as MainViewModel)?.Tick();
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) _live.Start();
            else _live.Stop();
        };
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
