using System.Drawing;
using System.Windows.Forms;

namespace WoWBoxer.Layouts.App;

/// <summary>The tray menu in the War Table colours (WinForms is only used for the tray icon).</summary>
internal sealed class TrayMenuStyle : ToolStripProfessionalRenderer
{
    private static readonly Color Panel = Color.FromArgb(0x15, 0x1A, 0x24), Gold = Color.FromArgb(0xC9, 0xA2, 0x4A),
        Text = Color.FromArgb(0xD6, 0xCD, 0xB6), Highlight = Color.FromArgb(0x3A, 0x2E, 0x14), GoldHi = Color.FromArgb(0xFF, 0xD1, 0x00);

    public TrayMenuStyle() : base(new Colors()) => RoundedEdges = false;

    public static void Apply(ContextMenuStrip menu)
    {
        menu.Renderer = new TrayMenuStyle();
        menu.BackColor = Panel;
        menu.ForeColor = Text;
        menu.ShowImageMargin = false;
        menu.Font = new Font("Segoe UI", 10f);
        foreach (ToolStripItem item in menu.Items) item.Padding = new Padding(4, 3, 4, 3);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected ? GoldHi : Text;
        base.OnRenderItemText(e);
    }

    private sealed class Colors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Panel;
        public override Color MenuBorder => Gold;
        public override Color MenuItemBorder => Gold;
        public override Color MenuItemSelected => Highlight;
        public override Color MenuItemSelectedGradientBegin => Highlight;
        public override Color MenuItemSelectedGradientEnd => Highlight;
        public override Color SeparatorDark => Color.FromArgb(0x52, 0xC9, 0xA2, 0x4A);
        public override Color SeparatorLight => Panel;
        public override Color ImageMarginGradientBegin => Panel;
        public override Color ImageMarginGradientMiddle => Panel;
        public override Color ImageMarginGradientEnd => Panel;
    }
}
