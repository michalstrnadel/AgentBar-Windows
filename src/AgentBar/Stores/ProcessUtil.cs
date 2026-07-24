using System.Diagnostics;

namespace AgentBar.Stores;

/// Liveness probe shared by SessionStore and RequestStore. Windows analogue of the
/// macOS `kill(pid, 0) == ESRCH` check. (PID reuse is possible but tolerated — same
/// as the macOS app.)
internal static class ProcessUtil
{
    public static bool IsAlive(int pid)
    {
        if (pid <= 0) return false;
        try { using var _ = Process.GetProcessById(pid); return true; }
        catch { return false; } // ArgumentException: no process with that id
    }
}
