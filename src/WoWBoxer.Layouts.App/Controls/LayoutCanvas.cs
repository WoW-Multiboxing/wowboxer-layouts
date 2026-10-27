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

public sealed class SlotsSwappedEventArgs(int a, int b) : EventArgs
{
    public int A { get; } = a;
    public int B { get; } = b;
}

/// <summary>
/// Draws the monitors to scale – exactly what will happen on screen, taskbar included – with the layout's windows
/// on top. Windows glide to new positions when the layout changes. In edit mode the user moves (drag), resizes
/// (handles), snaps (monitor and window edges; hold Alt to place freely) and nudges (arrow keys) windows.
/// <see cref="Compact"/> draws a small, non-interactive preview for the layout cards.
/// </summary>
public sealed class LayoutCanvas : FrameworkElement
{
    public static readonly DependencyProperty ProfileProperty = DependencyProperty.Register(
        nameof(Profile), typeof(LayoutProfile), typeof(LayoutCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((LayoutCanvas)d).StartAnimation()));
    public static readonly DependencyProperty MonitorsProperty = DependencyProperty.Register(
        nameof(Monitors), typeof(IReadOnlyList<MonitorInfo>), typeof(LayoutCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex), typeof(int), typeof(LayoutCanvas), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(LayoutCanvas), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ShowAllMonitorsProperty = DependencyProperty.Register(
        nameof(ShowAllMonitors), typeof(bool), typeof(LayoutCanvas), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MeasuredBarsProperty = DependencyProperty.Register(
        nameof(MeasuredBars), typeof(IReadOnlyDictionary<int, ViewModels.MeasuredBar>), typeof(LayoutCanvas), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty UiScaleProperty = DependencyProperty.Register(
        nameof(UiScale), typeof(double), typeof(LayoutCanvas), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AnimateProperty = DependencyProperty.Register(
        nameof(Animate), typeof(bool), typeof(LayoutCanvas), new PropertyMetadata(true));

    public LayoutProfile? Profile { get => (LayoutProfile?)GetValue(ProfileProperty); set => SetValue(ProfileProperty, value); }
    public IReadOnlyList<MonitorInfo>? Monitors { get => (IReadOnlyList<MonitorInfo>?)GetValue(MonitorsProperty); set => SetValue(MonitorsProperty, value); }
    public int SelectedIndex { get => (int)GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }
    public bool Animate { get => (bool)GetValue(AnimateProperty); set => SetValue(AnimateProperty, value); }
    /// <summary>False: draw only the monitors the layout uses (bigger), true: every monitor (to drag windows across).</summary>
    public bool ShowAllMonitors { get => (bool)GetValue(ShowAllMonitorsProperty); set => SetValue(ShowAllMonitorsProperty, value); }
    /// <summary>Slot index → what the open WoW window in that slot really draws. Those slots show the measured action-bar state (solid dot).</summary>
    public IReadOnlyDictionary<int, ViewModels.MeasuredBar>? MeasuredBars { get => (IReadOnlyDictionary<int, ViewModels.MeasuredBar>?)GetValue(MeasuredBarsProperty); set => SetValue(MeasuredBarsProperty, value); }
    /// <summary>WoW's UI Scale (1 = automatic), used for the planned state of slots without an open window (ring dot).</summary>
    public double UiScale { get => (double)GetValue(UiScaleProperty); set => SetValue(UiScaleProperty, value); }

    /// <summary>Raised when a drag or nudge has finished with the slot's new position.</summary>
    public event EventHandler<SlotEditedEventArgs>? SlotEdited;

    /// <summary>Raised when a window is dropped onto another one: swap their places.</summary>
    public event EventHandler<SlotsSwappedEventArgs>? SlotsSwapped;

    private const double HandleSize = 9, SnapScreenPixels = 10;
    private static readonly TimeSpan AnimationTime = TimeSpan.FromMilliseconds(320);

    // view transform: canvas = (pixel - origin) * scale + offset
    private double _scale = 1;
    private Point _offset;
    private int _originX, _originY;

    // drag state
    private int _dragIndex = -1, _hoverIndex = -1, _swapTarget = -1;
    private ResizeEdges _dragEdges;
    private PixelRect _dragStart, _dragRect;
    private Point _dragMouse;

    // glide animation: rectangles shown on screen move from where they were to where the profile says
    private Dictionary<int, Rect> _from = [], _shown = [];
    private DateTime _animStart;
    private bool _animating;

    public LayoutCanvas()
    {
        Focusable = true;
        FocusVisualStyle = null;
        ClipToBounds = true;
        UseLayoutRounding = false;   // smooth sub-pixel motion while gliding
    }

    private LayoutOptions Options => Profile?.Options ?? new LayoutOptions();
    private double Padding => Compact ? 4 : 18;
    private double LabelSpace => Compact ? 0 : 22;

    private sealed record Item(int Index, LayoutSlot Slot, MonitorInfo Monitor, PixelRect Raw, PixelRect Fitted);

    private List<Item> Items()
    {
        if (Profile is null || Monitors is not { Count: > 0 } monitors) return [];
        var list = new List<Item>();
        for (var i = 0; i < Profile.Slots.Count; i++)
        {
            var slot = Profile.Slots[i];
            var monitor = Core.Monitors.Find(monitors, slot.Monitor) ?? Core.Monitors.Primary(monitors);
            var raw = i == _dragIndex ? _dragRect : SlotEditing.Raw(slot, monitor, Options);
            list.Add(new Item(i, slot, monitor, raw, LayoutEngine.Fit(raw, slot.Fit ?? Options.Fit, slot.Anchor, Options.MinAspect, LayoutEngine.FrameSize(Options, monitor.Dpi))));
        }
        // overlays (always on top) are drawn last and hit first
        // overlays above the rest, and the window being edited above its neighbours
        return list.OrderBy(t => t.Slot.Z == ZOrder.Top ? 1 : 0).ThenBy(t => t.Index == SelectedIndex ? 1 : 0).ThenBy(t => t.Index).ToList();
    }

    /// <summary>The monitors to draw: all of them, or in a compact preview only those the layout uses.</summary>
    private IReadOnlyList<MonitorInfo> Shown()
    {
        if (Monitors is not { Count: > 0 } all || (ShowAllMonitors && !Compact) || Profile is null) return Monitors ?? [];
        var used = Profile.Slots.Select(s => (Core.Monitors.Find(all, s.Monitor) ?? Core.Monitors.Primary(all)).Index).ToHashSet();
        return all.Where(m => used.Contains(m.Index)).ToList();
    }

    private void UpdateTransform()
    {
        if (Shown() is not { Count: > 0 } monitors) return;
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

    // ---------- animation ----------

    private void StartAnimation()
    {
        if (!Animate || _dragIndex >= 0 || _shown.Count == 0 || !IsLoaded)
        {
            _from.Clear();
            return;
        }
        _from = new Dictionary<int, Rect>(_shown);
        _animStart = DateTime.UtcNow;
        if (!_animating)
        {
            _animating = true;
            CompositionTarget.Rendering += OnFrame;
        }
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        InvalidateVisual();
        if (DateTime.UtcNow - _animStart < AnimationTime) return;
        CompositionTarget.Rendering -= OnFrame;
        _animating = false;
        _from.Clear();
        InvalidateVisual();
    }

    /// <summary>Where a window is drawn this frame (gliding from its previous place, ease-out cubic).</summary>
    private Rect Shown(int index, Rect target)
    {
        if (!_animating || !_from.TryGetValue(index, out var from)) return target;
        var t = Math.Clamp((DateTime.UtcNow - _animStart).TotalMilliseconds / AnimationTime.TotalMilliseconds, 0, 1);
        var k = 1 - Math.Pow(1 - t, 3);
        return new Rect(from.X + (target.X - from.X) * k, from.Y + (target.Y - from.Y) * k,
            Math.Max(0, from.Width + (target.Width - from.Width) * k), Math.Max(0, from.Height + (target.Height - from.Height) * k));
    }

    // ---------- drawing ----------

    private static SolidColorBrush Brush(byte a, Color c) => new(Color.FromArgb(a, c.R, c.G, c.B));
    private static readonly Color Gold = Color.FromRgb(0xC9, 0xA2, 0x4A), GoldHi = Color.FromRgb(0xE8, 0xC6, 0x6A), Parchment = Color.FromRgb(0xE9, 0xDF, 0xC4);
    private static readonly FontFamily DisplayFont = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Cinzel Bold");
    private static readonly FontFamily BodyFont = new("Segoe UI Variable Text, Segoe UI");

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));   // whole area hit-testable
        var ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (Monitors is not { Count: > 0 } monitors)
        {
            if (!Compact) Draw(dc, Text("No monitors found.", 14, Brushes.Gray, ppd), new Point(Padding, Padding));
            return;
        }
        UpdateTransform();
        var radius = Compact ? 2.5 : 6;

        foreach (var m in Shown())
        {
            var bounds = ToCanvas(m.Bounds);
            // bezel + screen
            if (!Compact)
            {
                var bezel = bounds;
                bezel.Inflate(5, 5);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(0x07, 0x09, 0x0E)), new Pen(Brush(0x70, Gold), 1), bezel, radius + 3, radius + 3);
            }
            var screen = new LinearGradientBrush(Color.FromRgb(0x10, 0x15, 0x20), Color.FromRgb(0x08, 0x0B, 0x11), 90);
            dc.DrawRoundedRectangle(screen, new Pen(new SolidColorBrush(Color.FromRgb(0x3A, 0x45, 0x60)), Compact ? 0.8 : 1), bounds, radius, radius);

            // the taskbar band (bounds minus work area), drawn as a taskbar so nobody wonders about the strip
            foreach (var band in TaskbarBands(m))
            {
                var b = ToCanvas(band);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x1A, 0x1F, 0x2B)), null, b);
                if (!Compact && b.Height >= 10 && b.Width > 140)
                {
                    var dots = Math.Min(7, (int)(b.Width / 28));
                    var cx = b.X + b.Width / 2 - dots * 9;
                    for (var i = 0; i < dots; i++)
                        dc.DrawRoundedRectangle(Brush(0x55, Parchment), null, new Rect(cx + i * 18, b.Y + b.Height / 2 - 3, 9, 6), 1.5, 1.5);
                    var label = Text(Options.UseTaskbarArea ? "Taskbar (windows go under it)" : "Taskbar", 10, Brush(0x99, Parchment), ppd);
                    if (label.Width < b.Width / 2 - 70) Draw(dc, label, new Point(b.X + 8, b.Y + (b.Height - label.Height) / 2));
                }
            }

            if (!Compact)
            {
                var title = Text($"{m.Index}{(m.IsPrimary ? " · PRIMARY" : "")}  ·  {m.Bounds.Width}×{m.Bounds.Height}  ·  {m.ScalePercent} %", 11, Brush(0xD0, Gold), ppd, display: true);
                Draw(dc, title, new Point(bounds.X, bounds.Y - title.Height - 8));
            }
        }

        var items = Items();
        if (!Compact)
            foreach (var m in Shown().Where(m => items.All(i => i.Monitor.Index != m.Index)))
            {
                var b = ToCanvas(m.WorkArea);
                var hint = Text("Free – pick “2 monitors” to put followers here, or drag a window over", 12, Brush(0x90, Parchment), ppd);
                hint.MaxTextWidth = Math.Max(40, b.Width - 40);
                hint.TextAlignment = TextAlignment.Center;
                Draw(dc, hint, new Point(b.X + 20, b.Y + (b.Height - hint.Height) / 2));
            }
        var shown = new Dictionary<int, Rect>();
        foreach (var item in items)
        {
            var color = WowClasses.ColorFor(item.Slot.Class);
            var selected = item.Index == SelectedIndex && !Compact;
            var hovered = item.Index == _hoverIndex && !Compact && _dragIndex < 0;
            var target = ToCanvas(item.Fitted);
            var r = Shown(item.Index, target);
            shown[item.Index] = r;
            var overlay = item.Slot.Z == ZOrder.Top;

            if (item.Fitted != item.Raw && !Compact && !_animating)
                dc.DrawRectangle(null, new Pen(Brush(0x70, color), 1) { DashStyle = DashStyles.Dash }, ToCanvas(item.Raw));

            // shadow under floating / selected windows
            if (overlay || selected)
            {
                var s = r;
                s.Offset(0, Compact ? 1.5 : 4);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x90, 0, 0, 0)), null, s, 4, 4);
            }
            // body: panel gradient (main gets a radial "game" glow), class-colour top edge, gold frame when selected
            Brush body = item.Slot.Role == SlotRole.Main
                ? new RadialGradientBrush(Color.FromRgb(0x26, 0x33, 0x4D), Color.FromRgb(0x0E, 0x13, 0x1D)) { Center = new Point(0.5, 0.4), GradientOrigin = new Point(0.5, 0.4), RadiusX = 0.8, RadiusY = 0.9 }
                : new LinearGradientBrush(Color.FromRgb(0x22, 0x2A, 0x3E), Color.FromRgb(0x12, 0x17, 0x22), 70);
            var frame = selected ? new Pen(new SolidColorBrush(GoldHi), 2) : new Pen(hovered ? Brush(0xB0, Gold) : new SolidColorBrush(Color.FromRgb(0x2E, 0x38, 0x50)), hovered ? 1.5 : 1);
            if (selected)
            {
                var glow = r;
                glow.Inflate(3, 3);
                dc.DrawRoundedRectangle(null, new Pen(Brush(0x40, GoldHi), 5), glow, 6, 6);
            }
            dc.DrawRoundedRectangle(body, frame, r, Compact ? 1.5 : 3, Compact ? 1.5 : 3);
            var edge = Math.Max(Compact ? 2 : 3, Math.Min(5, r.Height * 0.03));
            if (r.Width > 2 && r.Height > edge)
                dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(r.X + 1, r.Y + 1, Math.Max(0, r.Width - 2), edge));

            if (Compact)
            {
                if (r.Height > 14 && r.Width > 10)
                {
                    var n = Text($"{item.Slot.N}", Math.Clamp(r.Height * 0.3, 8, 30), new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x00)), ppd, display: true);
                    Draw(dc, n, new Point(r.X + (r.Width - n.Width) / 2, r.Y + (r.Height - n.Height) / 2 + edge / 2));
                }
                continue;
            }

            // gold number medallion, class/character name, size, action-bar dot (app mockup v0.2)
            var (gw, gh) = LayoutEngine.GameArea(item.Fitted, LayoutEngine.FrameSize(Options, item.Monitor.Dpi));
            // what the action bar sees: measured on the open window, or planned (game area / UI Scale)
            var measured = MeasuredBars is { } bars && bars.TryGetValue(item.Index, out var bar) ? bar : null;
            var aspect = measured?.Effective ?? (gh == 0 ? 0 : (double)gw / gh / Math.Max(0.1, UiScale));
            var radiusM = Math.Clamp(Math.Min(r.Width, r.Height) * 0.16, 9, 26);
            var lines = new List<FormattedText>();
            var label = item.Slot.Name ?? WowClasses.Find(item.Slot.Class)?.Name;
            if (r.Height > 70 && label is not null) lines.Add(Text(label, 12.5, new SolidColorBrush(color), ppd, display: true));
            if (r.Height > 56 && r.Width > 80) lines.Add(Text($"{item.Fitted.Width} × {item.Fitted.Height}", 11, Brush(0xC8, Parchment), ppd));
            var showBar = r.Height > 84 && r.Width > 110;
            var barText = showBar ? Text(ActionBar.Short(aspect), 10.5, new SolidColorBrush(ActionBar.ColorFor(aspect)), ppd) : null;
            var total = radiusM * 2 + 6 + lines.Sum(l => l.Height) + (barText?.Height ?? 0);
            var y = r.Y + edge / 2 + (r.Height - total) / 2;
            var mc = new Point(r.X + r.Width / 2, y + radiusM);
            dc.DrawEllipse(new RadialGradientBrush(Color.FromRgb(0x23, 0x2B, 0x3E), Color.FromRgb(0x0B, 0x0E, 0x14)),
                new Pen(new LinearGradientBrush(Color.FromRgb(0xF7, 0xE3, 0xA6), Color.FromRgb(0x8C, 0x6B, 0x26), 90), Math.Max(1.5, radiusM * 0.12)), mc, radiusM, radiusM);
            var num = Text($"{item.Slot.N}", radiusM * 1.15, new SolidColorBrush(Color.FromRgb(0xFF, 0xD1, 0x00)), ppd, display: true);
            Draw(dc, num, new Point(mc.X - num.Width / 2, mc.Y - num.Height / 2), shadow: true);
            y += radiusM * 2 + 6;
            foreach (var line in lines)
            {
                Draw(dc, line, new Point(r.X + (r.Width - line.Width) / 2, y), shadow: true);
                y += line.Height;
            }
            if (barText is not null)
            {
                var dot = 7.0;
                var x0 = r.X + (r.Width - barText.Width - dot - 5) / 2;
                var barBrush = new SolidColorBrush(ActionBar.ColorFor(aspect));
                var centre = new Point(x0 + dot / 2, y + barText.Height / 2 + 0.5);
                if (measured is not null) dc.DrawEllipse(barBrush, null, centre, dot / 2, dot / 2);   // measured on the real window
                else dc.DrawEllipse(null, new Pen(barBrush, 1.4), centre, dot / 2 - 0.7, dot / 2 - 0.7);   // planned
                Draw(dc, barText, new Point(x0 + dot + 5, y), shadow: true);
            }
            if (overlay && r.Width > 50)
                Draw(dc, Text("ON TOP", 9, Brush(0xC0, GoldHi), ppd, display: true), new Point(r.X + 5, r.Y + edge + 3));

            if (item.Index == _swapTarget)
            {
                var t = r;
                t.Inflate(2, 2);
                dc.DrawRoundedRectangle(Brush(0x30, GoldHi), new Pen(new SolidColorBrush(GoldHi), 2.5) { DashStyle = DashStyles.Dash }, t, 4, 4);
                var swap = Text("SWAP", 13, new SolidColorBrush(GoldHi), ppd, display: true);
                Draw(dc, swap, new Point(t.X + (t.Width - swap.Width) / 2, t.Y + 8), shadow: true);
            }

            if (selected && !_animating)
                foreach (var (handle, _) in Handles(ToCanvas(item.Raw)))
                    dc.DrawRectangle(new SolidColorBrush(GoldHi), new Pen(Brushes.Black, 1), handle);
        }
        _shown = shown;
    }

    /// <summary>The parts of the monitor outside its work area (where the taskbar is).</summary>
    private static IEnumerable<PixelRect> TaskbarBands(MonitorInfo m)
    {
        PixelRect b = m.Bounds, w = m.WorkArea;
        if (w.Bottom < b.Bottom) yield return new PixelRect(b.X, w.Bottom, b.Width, b.Bottom - w.Bottom);
        if (w.Y > b.Y) yield return new PixelRect(b.X, b.Y, b.Width, w.Y - b.Y);
        if (w.X > b.X) yield return new PixelRect(b.X, b.Y, w.X - b.X, b.Height);
        if (w.Right < b.Right) yield return new PixelRect(w.Right, b.Y, b.Right - w.Right, b.Height);
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

    // ---------- editing ----------

    private (int Index, ResizeEdges Edges) HitTest(Point p)
    {
        var items = Items();
        var sel = items.FirstOrDefault(i => i.Index == SelectedIndex);
        if (sel is not null)
            foreach (var (handle, edges) in Handles(ToCanvas(sel.Raw)))
            {
                var grow = handle;
                grow.Inflate(4, 4);
                if (grow.Contains(p)) return (sel.Index, edges);
            }
        foreach (var item in Enumerable.Reverse(items))
            if (ToCanvas(item.Fitted).Contains(p) || ToCanvas(item.Raw).Contains(p)) return (item.Index, ResizeEdges.All);
        return (-1, ResizeEdges.None);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (Compact) return;
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
        if (Compact) return;
        var p = e.GetPosition(this);
        if (_dragIndex < 0)
        {
            var (index, edges) = HitTest(p);
            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                InvalidateVisual();
            }
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
        _swapTarget = _dragEdges == ResizeEdges.All ? SwapTargetAt(p) : -1;
        InvalidateVisual();
    }

    /// <summary>Another window under the pointer while moving one: dropping there swaps the two.</summary>
    private int SwapTargetAt(Point p)
    {
        foreach (var item in Enumerable.Reverse(Items()))
        {
            if (item.Index == _dragIndex) continue;
            var r = ToCanvas(item.Fitted);
            // only the middle half counts, so snapping against a neighbour's edge never turns into a swap
            r.Inflate(-r.Width / 4, -r.Height / 4);
            if (r.Contains(p)) return item.Index;
        }
        return -1;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_hoverIndex < 0) return;
        _hoverIndex = -1;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_dragIndex < 0) return;
        var index = _dragIndex;
        var rect = _dragRect;
        var changed = rect != _dragStart;
        var target = _swapTarget;
        _dragIndex = -1;
        _swapTarget = -1;
        ReleaseMouseCapture();
        if (target >= 0) SlotsSwapped?.Invoke(this, new SlotsSwappedEventArgs(index, target));
        else if (changed) Commit(index, rect);
        InvalidateVisual();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_dragIndex < 0) return;
        _dragIndex = -1;   // e.g. Escape or focus loss: cancel the drag
        _swapTarget = -1;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Compact || Profile is null || Monitors is null) return;
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
        _from.Clear();   // the user put it there – no glide
        SlotEdited?.Invoke(this, new SlotEditedEventArgs(index, SlotEditing.WithRect(Profile.Slots[index], rect, Monitors, Options)));
    }

    // ---------- text ----------

    private static FormattedText Text(string text, double size, Brush brush, double pixelsPerDip, bool display = false) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(display ? DisplayFont : BodyFont, FontStyles.Normal, display ? FontWeights.Normal : FontWeights.SemiBold, FontStretches.Normal),
            size, brush, pixelsPerDip);

    private static void Draw(DrawingContext dc, FormattedText text, Point at, bool shadow = false)
    {
        if (shadow)
        {
            var dark = text.BuildGeometry(new Point(at.X, at.Y + 1));
            dc.DrawGeometry(Brushes.Black, null, dark);
        }
        dc.DrawText(text, at);
    }
}
