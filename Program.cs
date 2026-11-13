using System;
using System.Threading;
using System.Windows;
using WF = System.Windows.Forms;

namespace RoboIsland;

static class Program
{
    [STAThread]
    static int Main(string[] a)
    {
        if (a.Length >= 2 && a[0] == "--hook") return Hooks.Run(a);

        using var mutex = new Mutex(true, "RoboIsland.Single", out var created);
        if (!created) return 0;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        bool first = !Settings.Exists;
        var s = Settings.Load();
        var w = new IslandWindow(s);

        var tray = new WF.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Visible = true, Text = "Robo Island" };
        w.Balloon = (t, m) => tray.ShowBalloonTip(4000, t, m, WF.ToolTipIcon.Info);

        SettingsWindow? sw = null;
        void OpenSettings()
        {
            if (sw != null) { sw.Activate(); return; }
            sw = new SettingsWindow(s, w.Reload);
            sw.Closed += (_, _) => sw = null;
            sw.Show();
        }

        var menu = new WF.ContextMenuStrip();
        menu.Items.Add(Loc.T("settings", s.Lang), null, (_, _) => OpenSettings());
        menu.Items.Add(Loc.T("quit", s.Lang), null, (_, _) => { tray.Visible = false; app.Shutdown(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => OpenSettings();

        w.Show();
        w.Reload();
        if (first) OpenSettings();
        return app.Run();
    }
}
