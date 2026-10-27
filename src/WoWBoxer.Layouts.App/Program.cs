using Velopack;

namespace WoWBoxer.Layouts.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Must run first: handles Velopack's install, update and uninstall hooks, then returns.
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
