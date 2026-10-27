using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WoWBoxer.Layouts.App.ViewModels;
using WoWBoxer.Layouts.Win32;

namespace WoWBoxer.Layouts.App.Views;

public partial class SetupView : UserControl
{
    private Point _dragStart;
    private PartyMember? _dragged;

    /// <summary>True while a squad row is being dragged (the window's live refresh pauses meanwhile).</summary>
    public static bool Dragging { get; private set; }

    public SetupView()
    {
        InitializeComponent();
        Canvas.SlotEdited += (_, e) => (DataContext as MainViewModel)?.OnSlotEdited(e.Index, e.Slot);
        Canvas.SlotsSwapped += (_, e) => (DataContext as MainViewModel)?.SwapSlots(e.A, e.B);
    }

    /// <summary>The saved layout's "⋯" opens its menu on a normal click.</summary>
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

    /// <summary>
    /// Typing in a row's name box (or tabbing into its details) selects that row and its window on the map – and keeps
    /// it selected when the focus moves on (a style trigger would undo the selection and close the details).
    /// </summary>
    private void OnRowFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is DependencyObject element && ItemsControl.ContainerFromElement(PartyList, element) is ListBoxItem { DataContext: PartyMember row }
            && DataContext is MainViewModel vm && vm.SelectedSlotIndex != row.Index)
            vm.SelectedSlotIndex = row.Index;
    }

    // ---------- drag squad rows by their handle to reorder (rows, map and real windows follow) ----------

    private void OnHandleDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragged = (sender as FrameworkElement)?.Tag as PartyMember;
        // The list would capture the mouse to select the row, and then the handle never sees the drag move.
        e.Handled = true;
        ((FrameworkElement)sender).CaptureMouse();
    }

    private void OnHandleUp(object sender, MouseButtonEventArgs e)
    {
        ((FrameworkElement)sender).ReleaseMouseCapture();
        if (_dragged is { } row && DataContext is MainViewModel vm) vm.SelectedSlotIndex = row.Index;   // a click on ⋮⋮ selects the row
        _dragged = null;
    }

    private void OnHandleMove(object sender, MouseEventArgs e)
    {
        if (_dragged is null || e.LeftButton != MouseButtonState.Pressed) return;
        var d = e.GetPosition(this) - _dragStart;
        if (Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance && Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance) return;
        var row = _dragged;
        _dragged = null;
        ((FrameworkElement)sender).ReleaseMouseCapture();   // DoDragDrop takes over the mouse
        Dragging = true;   // keep the list still while dragging
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(PartyMember), row), DragDropEffects.Move);
        }
        finally
        {
            Dragging = false;
        }
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
        {
            vm.MoveCharacter(dragged.Index, target.Index);
            vm.SelectedSlotIndex = target.Index;   // the moved character stays selected
        }
        e.Handled = true;
    }
}
