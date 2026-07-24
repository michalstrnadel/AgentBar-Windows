using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace AgentBar.Input;

/// System-wide Allow/Deny hotkeys via Win32 RegisterHotKey, delivered to a message-only
/// window, on the global Ctrl+Alt+A (allow) / Ctrl+Alt+D (deny) hotkeys. Fires even when
/// AgentBar isn't focused; needs no special
/// permission. Opt-in — the tray menu toggles it.
public sealed class HotKeyCenter : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_NOREPEAT = 0x4000;
    private const uint VK_A = 0x41, VK_D = 0x44;
    private const int ID_ALLOW = 1, ID_DENY = 2;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    private HwndSource? _source;
    private Action? _allow;
    private Action? _deny;

    /// Turn the Allow/Deny hotkeys on or off. Must be called on the UI thread.
    public void SetEnabled(bool enabled, Action allow, Action deny)
    {
        Disable();
        if (!enabled) return;

        _allow = allow;
        _deny = deny;

        var parameters = new HwndSourceParameters("AgentBarHotKeys")
        {
            Width = 0,
            Height = 0,
            ParentWindow = HWND_MESSAGE, // message-only window: no visible surface
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);

        var mods = MOD_CONTROL | MOD_ALT | MOD_NOREPEAT;
        RegisterHotKey(_source.Handle, ID_ALLOW, mods, VK_A);
        RegisterHotKey(_source.Handle, ID_DENY, mods, VK_D);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;
        switch (wParam.ToInt32())
        {
            case ID_ALLOW: _allow?.Invoke(); handled = true; break;
            case ID_DENY: _deny?.Invoke(); handled = true; break;
        }
        return IntPtr.Zero;
    }

    private void Disable()
    {
        if (_source is null) return;
        UnregisterHotKey(_source.Handle, ID_ALLOW);
        UnregisterHotKey(_source.Handle, ID_DENY);
        _source.RemoveHook(WndProc);
        _source.Dispose();
        _source = null;
    }

    public void Dispose() => Disable();

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
