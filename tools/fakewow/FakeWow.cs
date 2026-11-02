// A stand-in "WoW client" window for tests: a plain window that never takes focus. Args: x y w h [title] [keylog]
// With a keylog file it writes every key message it receives (WM_KEYDOWN / WM_SYSKEYDOWN, as hex virtual-key codes),
// so tools\swap-check.ps1 can prove that a window-swap hotkey never reaches the "game".
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

class FakeWow : Form, IMessageFilter
{
    readonly string keyLog;

    protected override bool ShowWithoutActivation { get { return true; } }

    FakeWow(int x, int y, int w, int h, string title, string keyLog)
    {
        this.keyLog = keyLog;
        Text = title;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(x, y, w, h);
        BackColor = Color.FromArgb(30, 40, 60);
        var label = new Label { Text = title + "\n(test window, WoWBoxer)", ForeColor = Color.Gold, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16) };
        Controls.Add(label);
        if (keyLog != null) Application.AddMessageFilter(this);
    }

    // Every key message posted to this window's thread (whichever control has focus).
    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == 0x100 || m.Msg == 0x104)
            File.AppendAllText(keyLog, string.Format("{0} {1:X2}\r\n", m.Msg == 0x104 ? "SYSKEYDOWN" : "KEYDOWN", (int)m.WParam));
        return false;
    }

    [STAThread]
    static void Main(string[] a)
    {
        if (a.Length < 4) a = new[] { "2700", "300", "800", "600", "Fake WoW (opened by the app)" };
        Application.Run(new FakeWow(int.Parse(a[0]), int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3]), a.Length > 4 ? a[4] : "Fake WoW", a.Length > 5 ? a[5] : null));
    }
}
