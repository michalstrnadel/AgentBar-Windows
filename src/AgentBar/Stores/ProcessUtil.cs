using System.Diagnostics;

namespace AgentBar.Stores;

/// Liveness probe shared by SessionStore and RequestStore — does a process with this id
/// exist? (PID reuse is possible but tolerated.)
internal static class ProcessUtil
{
    public static bool IsAlive(int pid)
    {
        if (pid <= 0) return false;
        try { using var _ = Process.GetProcessById(pid); return true; }
        catch { return false; } // ArgumentException: no process with that id
    }
}
