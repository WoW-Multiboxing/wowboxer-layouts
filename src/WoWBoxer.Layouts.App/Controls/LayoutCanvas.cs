using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WoWBoxer.Layouts.Core;

namespace WoWBoxer.Layouts.App.Controls;

public sealed class SlotEditedEventArgs(int index, LayoutSlot slot) : EventArgs
{
    public int Index { get; } = index;
    public LayoutSlot Slot { get; } = slot;
}

/// <summary>
/// Draws the monitors to scale with the layout's slots on top, and lets the user move (drag), resize (handles),
/// snap (edges of monitors and other slots; hold Alt to place freely) and nudge (arrow keys) slots.
/// </summary>
public sealed class LayoutCanvas : FrameworkElement
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(LayoutProfile), typeof(LayoutCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MonitorsProperty = DependencyProperty.Register(
        nameof(Monitors), typeof(IReadOnlyList<MonitorInfo>), typeof(LayoutCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(LayoutCanvas), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public LayoutProfile? Profile { get => (LayoutProfile?)GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    public IReadOnlyList<MonitorInfo>? Monitors { get => (IReadOnlyList<MonitorInfo>?)GetValue(MonitorsProperty); set => SetValue(MonitorsProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>Raised when a drag or nudge has finished with the slot's new position.</summary>
    public event EventHandler<SlotEditedEventArgs>? SlotEdited;

    public static readonly Color[] Palette =
    [
        Color.FromRgb(0x4D, 0xA3, 0xFF), Color.FromRgb(0xF0, 0xB4, 0x3C), Color.FromRgb(0x5B, 0xD0, 0x8A), Color.FromRgb(0xB4, 0x8C, 0xFF),
        Color.FromRgb(0xFF, 0x6B, 0x6B), Color.FromRgb(0x3C, 0xD2, 0xD2), Color.FromRgb(0xFF, 0x9F, 0x43), Color.FromRgb(0xFF, 0x7A, 0xC6),
    ];

    private const double Padding = 18, LabelSpace = 20, HandleSize = 9, SnapScreenPixels = 10;

    // view transform: canvas = (pixel - origin) * scale + offset
    private double _scale = 1;
    private Point _offset;
    private int _originX, _originY;

    // drag state
    private int _dragIndex = -1;
    private ResizeEdges _dragEdges;
    private PixelRect _dragStart, _dragRect;
    private Point _dragMouse;

    public LayoutCanvas()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
    }

    private LayoutOptions Options => Profile?.Options ?? new LayoutOptions();

    private IReadOnlyList<(int Index, LayoutSlot Slot, MonitorInfo Monitor, PixelRect Raw)> Items()
    {
        if (Profile is null || Monitors is not { Count: > 0 } monitors) return [];
        var list = new List<(int, LayoutSlot, MonitorInfo, PixelRect)>();
        for (var i = 0; i < Profile.Slots.Count; i++)
        {
            var slot = Profile.Slots[i];
            var monitor = Core.Monitors.Find(monitors, slot.Monitor) ?? Core.Monitors.Primary(monitors);
            var raw = i == _dragIndex ? _dragRect : SlotEditing.Raw(slot, monitor, Options);
            list.Add((i, slot, monitor, raw));
        }
        // overlays (always on top) are drawn last and hit first
        return list.OrderBy(t => t.Item2.Z == ZOrder.Top ? 1 : 0).ThenBy(t => t.Item1).ToList();
    }

    private void UpdateTransform()
    {
        if (Monitors is not { Count: > 0 } monitors) return;
        int left = monitors.Min(m => m.Bounds.X), top = monitors.Min(m => m.Bounds.Y);
        int right = monitors.Max(m => m.Bounds.Right), bottom = monitors.Max(m => m.Bounds.Bottom);
        var w = Math.Max(ActualWidth - 2 * Padding, 10);
        var h = Math.Max(ActualHeight - 2 * Padding - LabelSpace, 10);
        _scale = Math.Min(w / (right - left), h / (bottom - top));
        _originX = left;
        _originY = top;
        _offset = new Point(Padding + (w - (right - left) * _scale) / 2, Padding + LabelSpace + (h - (bottom - top) * _scale) / 2);
    }

    private Rect ToCanvas(PixelRect r) =>
        new((r.X - _originX) * _scale + _offset.X, (r.Y - _originY) * _scale + _offset.Y, r.Width * _scale, r.Height * _scale);

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));   // makes the whole area hit-testable
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (Monitors is not { Count: > 0 } monitors)
        {
            DrawText(dc, "No monitors found.", new Point(Padding, Padding), 14, Brushes.Gray, pixelsPerDip);
            return;
        }
        UpdateTransform();

        var monitorFill = new SolidColorBrush(Color.FromRgb(0x22, 0x26, 0x30));
        var taskbarFill = new SolidColorBrush(Color.FromRgb(0x17, 0x19, 0x20));
        var monitorPen = new Pen(new SolidColorBrush(Color.FromRgb(0x4A, 0x50, 0x5E)), 1);
        var labelBrush = new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB5));
        foreach (var m in monitors)
        {
            var bounds = ToCanvas(m.Bounds);
            dc.DrawRoundedRectangle(taskbarFill, null, bounds, 6, 6);
            var area = ToCanvas(m.Area(Options.UseTaskbarArea));
            dc.DrawRectangle(monitorFill, null, area);
            dc.DrawRoundedRectangle(null, monitorPen, bounds, 6, 6);
            DrawText(dc, $"{m.Index}{(m.IsPrimary ? " · primary" : "")} · {m.Bounds.Width}×{m.Bounds.Height} · {m.ScalePercent} %",
                new Point(bounds.X + 2, bounds.Y - 18), 11, labelBrush, pixelsPerDip);
        }

        var items = Items();
        foreach (var (index, slot, _, raw) in items)
        {
            var color = Palette[index % Palette.Length];
            var fitted = LayoutEngine.Fit(raw, slot.Fit ?? Options.Fit, slot.Anchor, Options.MinAspect);
            var selected = index == SelectedIndex;
            var stroke = new Pen(new SolidColorBrush(color), selected ? 2.5 : 1.4);
            var fill = new SolidColorBrush(Color.FromArgb(selected ? (byte)0x70 : (byte)0x48, color.R, color.G, color.B));

            var rawRect = ToCanvas(raw);
            if (fitted != raw)
            {
                var dashed = new Pen(new SolidColorBrush(Color.FromArgb(0x90, color.R, color.G, color.B)), 1) { DashStyle = DashStyles.Dash };
                dc.DrawRectangle(null, dashed, rawRect);
            }
            var r = ToCanvas(fitted);
            dc.DrawRoundedRectangle(fill, stroke, r, 3, 3);

            // number, name, size and aspect
            var big = Math.Clamp(r.Height * 0.32, 12, 44);
            var center = new Point(r.X + r.Width / 2, r.Y + r.Height / 2);
            var number = Text($"{slot.N}", big, Brushes.White, pixelsPerDip, FontWeights.SemiBold);
            var lines = new List<FormattedText> { number };
            if (r.Height > 60 && slot.Name is { Length: > 0 }) lines.Add(Text(slot.Name, 12, Brushes.White, pixelsPerDip));
            if (r.Height > 46 && r.Width > 70)
            {
                var aspect = fitted.Height == 0 ? 0 : (double)fitted.Width / fitted.Height;
                // green = full Classic action bar (4:3+), amber = bar ends clipped, red = unusually narrow
                var tone = aspect >= 4.0 / 3 - 0.001 ? Color.FromRgb(0x8E, 0xE6, 0xA8) : aspect >= 1.15 ? Color.FromRgb(0xF5, 0xD0, 0x7A) : Color.FromRgb(0xFF, 0x8A, 0x8A);
                lines.Add(Text($"{fitted.Width}×{fitted.Height} · {aspect.ToString("0.00", CultureInfo.InvariantCulture)}", 11, new SolidColorBrush(tone), pixelsPerDip));
            }
            var total = lines.Sum(l => l.Height);
            var y = center.Y - total / 2;
            foreach (var line in lines)
            {
                dc.DrawText(line, new Point(center.X - line.Width / 2, y));
                y += line.Height;
            }
            if (slot.Z == ZOrder.Top && r.Width > 40)
                DrawText(dc, "on top", new Point(r.X + 4, r.Y + 2), 10, Brushes.White, pixelsPerDip);

            if (selected)
                foreach (var (handle, _) in Handles(rawRect))
                    dc.DrawRectangle(Brushes.White, new Pen(new SolidColorBrush(color), 1.5), handle);
        }
    }

    private static IEnumerable<(Rect Handle, ResizeEdges Edges)> Handles(Rect r)
    {
        var s = HandleSize;
        double l = r.Left - s / 2, t = r.Top - s / 2, cx = r.Left + r.Width / 2 - s / 2, cy = r.Top + r.Height / 2 - s / 2, rr = r.Right - s / 2, b = r.Bottom - s / 2;
        yield return (new Rect(l, t, s, s), ResizeEdges.Left | ResizeEdges.Top);
        yield return (new Rect(rr, t, s, s), ResizeEdges.Right | ResizeEdges.Top);
        yield return (new Rect(l, b, s, s), ResizeEdges.Left | ResizeEdges.Bottom);
        yield return (new Rect(rr, b, s, s), ResizeEdges.Right | ResizeEdges.Bottom);
        yield return (new Rect(cx, t, s, s), ResizeEdges.Top);
        yield return (new Rect(cx, b, s, s), ResizeEdges.Bottom);
        yield return (new Rect(l, cy, s, s), ResizeEdges.Left);
        yield return (new Rect(rr, cy, s, s), ResizeEdges.Right);
    }

    private (int Index, ResizeEdges Edges) HitTest(Point p)
    {
        var items = Items();
        var sel = items.FirstOrDefault(i => i.Index == SelectedIndex);
        if (SelectedIndex >= 0 && sel.Slot is not null)
            foreach (var (handle, edges) in Handles(ToCanvas(sel.Raw)))
            {
                var grow = handle;
                grow.Inflate(3, 3);
                if (grow.Contains(p)) return (sel.Index, edges);
            }
        foreach (var item in items.Reverse())
            if (ToCanvas(item.Raw).Contains(p)) return (item.Index, ResizeEdges.All);
        return (-1, ResizeEdges.None);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        var (index, edges) = HitTest(e.GetPosition(this));
        SelectedIndex = index;
        if (index < 0 || Profile is null) return;
        var item = Items().First(i => i.Index == index);
        _dragIndex = index;
        _dragEdges = edges;
        _dragStart = _dragRect = item.Raw;
        _dragMouse = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var p = e.GetPosition(this);
        if (_dragIndex < 0)
        {
            var (_, edges) = HitTest(p);
            Cursor = edges switch
            {
                ResizeEdges.All => Cursors.SizeAll,
                ResizeEdges.Left or ResizeEdges.Right => Cursors.SizeWE,
                ResizeEdges.Top or ResizeEdges.Bottom => Cursors.SizeNS,
                ResizeEdges.Left | ResizeEdges.Top or ResizeEdges.Right | ResizeEdges.Bottom => Cursors.SizeNWSE,
                ResizeEdges.None => Cursors.Arrow,
                _ => Cursors.SizeNESW,
            };
            return;
        }
        var dx = (int)Math.Round((p.X - _dragMouse.X) / _scale);
        var dy = (int)Math.Round((p.Y - _dragMouse.Y) / _scale);
        var rect = SlotEditing.Drag(_dragStart, _dragEdges, dx, dy);
        if (!Keyboard.IsKeyDown(Key.LeftAlt) && !Keyboard.IsKeyDown(Key.RightAlt))
        {
            var others = Items().Where(i => i.Index != _dragIndex).Select(i => i.Raw);
            var (xs, ys) = SlotEditing.SnapLines(others, Monitors!, Options.UseTaskbarArea);
            rect = SlotEditing.Snap(rect, _dragEdges, xs, ys, (int)Math.Ceiling(SnapScreenPixels / _scale));
        }
        _dragRect = rect;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragIndex < 0) return;
        var index = _dragIndex;
        var rect = _dragRect;
        var changed = rect != _dragStart;
        _dragIndex = -1;
        ReleaseMouseCapture();
        if (changed) Commit(index, rect);
        InvalidateVisual();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_dragIndex >= 0)
        {
            _dragIndex = -1;   // e.g. Escape or focus loss: cancel the drag
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Profile is null || Monitors is null) return;
        if (e.Key == Key.Escape && _dragIndex >= 0)
        {
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab && Profile.Slots.Count > 0)
        {
            var back = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            SelectedIndex = ((SelectedIndex < 0 ? (back ? 0 : -1) : SelectedIndex) + (back ? -1 : 1) + Profile.Slots.Count) % Profile.Slots.Count;
            e.Handled = true;
            return;
        }
        if (SelectedIndex < 0 || SelectedIndex >= Profile.Slots.Count) return;
        int dx = 0, dy = 0;
        switch (e.Key)
        {
            case Key.Left: dx = -1; break;
            case Key.Right: dx = 1; break;
            case Key.Up: dy = -1; break;
            case Key.Down: dy = 1; break;
            default: return;
        }
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        var item = Items().First(i => i.Index == SelectedIndex);
        var rect = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            ? SlotEditing.Drag(item.Raw, ResizeEdges.Right | ResizeEdges.Bottom, dx * step, dy * step)   // Ctrl+arrows: resize
            : SlotEditing.Drag(item.Raw, ResizeEdges.All, dx * step, dy * step);
        Commit(SelectedIndex, rect);
        e.Handled = true;
    }

    private void Commit(int index, PixelRect rect)
    {
        if (Profile is null || Monitors is null) return;
        SlotEdited?.Invoke(this, new SlotEditedEventArgs(index, SlotEditing.WithRect(Profile.Slots[index], rect, Monitors, Options)));
    }

    private static FormattedText Text(string text, double size, Brush brush, double pixelsPerDip, FontWeight? weight = null) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
            size, brush, pixelsPerDip);

    private static void DrawText(DrawingContext dc, string text, Point at, double size, Brush brush, double pixelsPerDip) =>
        dc.DrawText(Text(text, size, brush, pixelsPerDip), at);
}
