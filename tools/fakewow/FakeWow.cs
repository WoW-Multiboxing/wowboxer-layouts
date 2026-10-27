// A stand-in "WoW client" window for tests: a plain window that never takes focus. Args: x y w h [title]
using System;
using System.Drawing;
using System.Windows.Forms;

class FakeWow : Form
{
    protected override bool ShowWithoutActivation { get { return true; } }

    FakeWow(int x, int y, int w, int h, string title)
    {
        Text = title;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(x, y, w, h);
        BackColor = Color.FromArgb(30, 40, 60);
        var label = new Label { Text = title + "\n(test window, WoWBoxer)", ForeColor = Color.Gold, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16) };
        Controls.Add(label);
    }

    [STAThread]
    static void Main(string[] a)
    {
        if (a.Length < 4) a = new[] { "2700", "300", "800", "600", "Fake WoW (opened by the app)" };
        Application.Run(new FakeWow(int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3]), a.Length > 4 ? a[4] : "Fake WoW"));
    }
}
