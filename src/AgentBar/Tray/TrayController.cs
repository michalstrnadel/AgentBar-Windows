using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using AgentBar.Models;
using AgentBar.Rendering;
using AgentBar.Stores;

namespace AgentBar.Tray;

/// Owns the notification-area icon and the popover. Windows analogue of the macOS
/// StatusItemController: no main window, no taskbar button, no dock/alt-tab presence.
public sealed class TrayController : IDisposable
{
    private readonly TaskbarIcon _icon = new();
    private readonly SessionStore _sessionStore = new();
    private readonly RequestStore _requestStore = new();
    private readonly PopoverWindow _popover = new();

    private IReadOnlyList<Session> _sessions = Array.Empty<Session>();
    private IReadOnlyList<ApprovalRequest> _approvals = Array.Empty<ApprovalRequest>();

    public void Start()
    {
        _icon.ToolTipText = "AgentBar";
        _icon.IconSource = IconRenderer.Render(_sessions);
        _icon.TrayLeftMouseUp += (_, _) => TogglePopover();
        _icon.ContextMenu = BuildMenu();

        _sessionStore.Changed += OnSessionsChanged;
        _requestStore.Changed += OnRequestsChanged;
        _sessionStore.Start();
        _requestStore.Start();
    }

    private void OnSessionsChanged(IReadOnlyList<Session> sessions)
    {
        _sessions = sessions;
        _icon.IconSource = IconRenderer.Render(sessions);
        _icon.ToolTipText = sessions.Count == 0
            ? "AgentBar — idle"
            : $"AgentBar — {sessions.Count} active";
        RefreshPopoverIfVisible();
    }

    private void OnRequestsChanged(IReadOnlyList<ApprovalRequest> approvals)
    {
        _approvals = approvals;
        RefreshPopoverIfVisible();
    }

    /// Called by an approval button: record the decision the blocking hook is waiting for.
    private void Answer(ApprovalRequest req, string behavior)
    {
        var rule = behavior == "always" ? req.RuleSuggestionRaw : null;
        AnswerWriter.Write(behavior, rule, req);

        // Drop the card immediately; the hook removes the request file on pickup and the
        // store's next refresh will confirm it's gone.
        _approvals = _approvals.Where(a => a.FileName != req.FileName).ToList();
        RefreshPopoverIfVisible();
    }

    private void TogglePopover()
    {
        if (_popover.IsVisible)
        {
            _popover.Hide();
            return;
        }
        // A click on the tray icon while the popover is open first fires the window's
        // Deactivated (which hides it), so by now IsVisible is already false. Treat a
        // click that lands right after that auto-hide as "close", not "reopen".
        if (DateTime.UtcNow - _popover.LastHidden < TimeSpan.FromMilliseconds(250)) return;

        _popover.Update(_sessions, _approvals, Answer);
        _popover.ShowNearTray();
    }

    private void RefreshPopoverIfVisible()
    {
        if (_popover.IsVisible) _popover.Update(_sessions, _approvals, Answer);
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var quit = new MenuItem { Header = "Quit AgentBar" };
        quit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(quit);
        return menu;
    }

    public void Dispose()
    {
        _sessionStore.Dispose();
        _requestStore.Dispose();
        _icon.Dispose();
    }
}
