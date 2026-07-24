using System;
using System.Windows.Threading;
using AgentBar.Models;

namespace AgentBar.Input;

/// Best-effort approval for agents without decision hooks (Codex, Copilot): bring the
/// session's terminal window forward, then send the agent's approval keystroke.
/// Honestly labelled in the UI as "sends keystroke" — delivery to the right tab is not
/// guaranteed (one window can host many tabs). SendInput also can't reach an elevated
/// window from a non-elevated app.
public static class KeystrokeApprover
{
    public static void Approve(Session session, ushort[] keys)
    {
        var hwnd = TerminalWindow.FindForProcess(session.Pid);
        if (hwnd == IntPtr.Zero) return; // no window located — best-effort ends here

        TerminalWindow.BringToForeground(hwnd);

        // Give the window time to come forward before typing into it (~0.7s).
        var timer = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(700),
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var key in keys) Keyboard.Tap(key);
        };
        timer.Start();
    }
}
