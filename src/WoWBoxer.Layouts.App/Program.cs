using Velopack;

namespace WoWBoxer.Layouts.App;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        // Must run first: handles Velopack's install, update and uninstall hooks, then returns.
        // Uninstall: put the user's own hover-to-activate values back if the app changed them.
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => ViewModels.HoverViewModel.RestoreOnUninstall())
            .Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
