using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WoWBoxer.Layouts.App.ViewModels;

namespace WoWBoxer.Layouts.App.Views;

public partial class SquadView : UserControl
{
    // While the tab is open, statuses and the checklist follow what happens (windows opening, Battle.net starting).
    private readonly DispatcherTimer _live = new() { Interval = TimeSpan.FromSeconds(2) };
    private Point _dragStart;
    private PartyMember? _dragged;

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

    // ---------- drag rows by their handle to reorder the squad ----------

    private void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragged = (sender as FrameworkElement)?.Tag as PartyMember;
    }

    private void OnHandleMove(object sender, MouseEventArgs e)
    {
        if (_dragged is null || e.LeftButton != MouseButtonState.Pressed) return;
        var d = e.GetPosition(this) - _dragStart;
        if (Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance && Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance) return;
        var row = _dragged;
        _dragged = null;
        _live.Stop();   // keep the list still while dragging
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(PartyMember), row), DragDropEffects.Move);
        _live.Start();
        if (DataContext is MainViewModel vm) foreach (var p in vm.Party) p.IsDropTarget = false;
    }

    private static PartyMember? Target(object sender) => (sender as FrameworkElement)?.Tag as PartyMember;

    private void OnRowDragOver(object sender, DragEventArgs e)
    {
        var dragged = e.Data.GetData(typeof(PartyMember)) as PartyMember;
        var target = Target(sender);
        e.Effects = dragged is not null && target is not null && dragged != target ? DragDropEffects.Move : DragDropEffects.None;
        if (DataContext is MainViewModel vm)
            foreach (var p in vm.Party) p.IsDropTarget = p == target && e.Effects == DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnRowDragLeave(object sender, DragEventArgs e)
    {
        if (Target(sender) is { } t) t.IsDropTarget = false;
    }

    private void OnRowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(PartyMember)) is PartyMember dragged && Target(sender) is { } target && DataContext is MainViewModel vm)
            vm.MoveCharacter(dragged.Index, target.Index);
        e.Handled = true;
    }
}
