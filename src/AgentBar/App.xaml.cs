using System;
using System.IO;
using System.Threading;
using System.Windows;
using AgentBar.Stores;
using AgentBar.Tray;

namespace AgentBar;

/// Tray-only application entry point: no main window, no taskbar button, lives entirely
/// in the notification area.
public partial class App : Application
{
    private static Mutex? _instanceLock;
    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One tray icon per user session; a second launch (e.g. a hook relaunch race) exits.
        _instanceLock = new Mutex(initiallyOwned: true, "AgentBar.SingleInstance", out var isNew);
        if (!isNew) { Shutdown(); return; }

        Paths.EnsureDirectories();
        // Record where we live so the hooks can relaunch us (Windows has no `open -b`).
        try { File.WriteAllText(Paths.AppPathMarker, Environment.ProcessPath ?? ""); } catch { /* best effort */ }

        _tray = new TrayController();
        _tray.Start();

        // Idempotent: refresh the bundled hook scripts and wire them into each agent's
        // config. Runs on a background thread; failures retry next launch.
        HookInstaller.InstallIfNeeded();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _instanceLock?.Dispose();
        base.OnExit(e);
    }
}
