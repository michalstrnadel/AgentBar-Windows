using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AgentBar.Input;

/// Locates the terminal window hosting an agent process and brings it forward.
/// A CLI agent (Codex, Copilot) is a console process with no window of its own — the
/// visible window belongs to an ancestor (Windows Terminal, conhost, …). We walk up
/// the parent-process chain until we find one that owns a visible top-level window.
internal static class TerminalWindow
{
    private const int MaxAncestors = 6;

    public static IntPtr FindForProcess(int pid)
    {
        if (pid <= 0) return IntPtr.Zero;
        var parents = BuildParentMap();
        var probe = pid;
        for (var depth = 0; depth < MaxAncestors && probe > 0; depth++)
        {
            var hwnd = TopLevelVisibleWindowOf((uint)probe);
            if (hwnd != IntPtr.Zero) return hwnd;
            probe = parents.TryGetValue(probe, out var parent) ? parent : 0;
        }
        return IntPtr.Zero;
    }

    /// Restore-if-minimized, then steal focus. AttachThreadInput defeats the foreground
    /// lock that would otherwise silently drop a SetForegroundWindow from a background app.
    public static void BringToForeground(IntPtr hwnd)
    {
        if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);

        var foreground = GetForegroundWindow();
        var targetThread = GetWindowThreadProcessId(hwnd, out _);
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var attached = foregroundThread != targetThread &&
                       AttachThreadInput(foregroundThread, targetThread, true);
        SetForegroundWindow(hwnd);
        if (attached) AttachThreadInput(foregroundThread, targetThread, false);
    }

    private static Dictionary<int, int> BuildParentMap()
    {
        var map = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE) return map;
        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(snapshot, ref entry))
            {
                do { map[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID; }
                while (Process32Next(snapshot, ref entry));
            }
        }
        finally { CloseHandle(snapshot); }
        return map;
    }

    private static IntPtr TopLevelVisibleWindowOf(uint pid)
    {
        var found = IntPtr.Zero;
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero) return true; // owned/secondary window
            GetWindowThreadProcessId(hwnd, out var owner);
            if (owner != pid) return true;
            found = hwnd;
            return false; // stop enumeration
        }, IntPtr.Zero);
        return found;
    }

    // MARK: - Interop

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);
    private const uint GW_OWNER = 4;
    private const int SW_RESTORE = 9;

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
}
