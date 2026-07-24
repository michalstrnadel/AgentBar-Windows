using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AgentBar.Models;
using AgentBar.Rendering;

namespace AgentBar.Tray;

/// Borderless panel anchored to the bottom-right, near the notification area.
/// The macOS app uses an NSMenu under the status item; on Windows we render our own.
public partial class PopoverWindow : Window
{
    public PopoverWindow() => InitializeComponent();

    /// When the window last auto-hid on losing focus. TrayController uses this to tell
    /// a "click to open" apart from the click that just dismissed the popover.
    public DateTime LastHidden { get; private set; } = DateTime.MinValue;

    public void Update(IReadOnlyList<Session> sessions)
    {
        var rows = sessions.Select(Row.From).ToList();
        List.ItemsSource = rows;
        EmptyLabel.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ShowNearTray()
    {
        // Realize off-screen first so ActualHeight is known before we place it.
        Opacity = 0;
        Left = -10000;
        Top = -10000;
        Show();
        Dispatcher.BeginInvoke(new Action(PositionNearTray), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// Anchor to the bottom-right of the monitor under the cursor, DPI-correct.
    private void PositionNearTray()
    {
        var work = TrayInterop.WorkAreaUnderCursor(); // device pixels, cursor's monitor
        var source = PresentationSource.FromVisual(this);
        if (work is { } wr && source?.CompositionTarget is { } target)
        {
            // Map the monitor's bottom-right corner from device pixels to WPF DIPs.
            var corner = target.TransformFromDevice.Transform(new Point(wr.Right, wr.Bottom));
            Left = corner.X - Width - 12;
            Top = corner.Y - ActualHeight - 12;
        }
        else
        {
            var wa = SystemParameters.WorkArea; // fallback: primary screen
            Left = wa.Right - Width - 12;
            Top = wa.Bottom - ActualHeight - 12;
        }
        Opacity = 1;
        Activate();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        LastHidden = DateTime.UtcNow;
        Hide();
    }

    /// View model for one row — keeps display formatting out of the XAML.
    public sealed class Row
    {
        public string AgentName { get; init; } = "";
        public string StateText { get; init; } = "";
        public string SubText { get; init; } = "";
        public Brush DotBrush { get; init; } = Brushes.Gray;

        public static Row From(Session s)
        {
            var branch = s.GitBranch;
            var place = s.Project;
            if (!string.IsNullOrEmpty(branch))
                place = string.IsNullOrEmpty(place) ? branch : $"{place} · {branch}";

            var sub = string.IsNullOrEmpty(s.Label)
                ? place
                : string.IsNullOrEmpty(place) ? s.Label : $"{s.Label} — {place}";

            var dot = new SolidColorBrush(StatusColors.For(s.State));
            dot.Freeze();

            return new Row
            {
                AgentName = s.Agent.Name,
                StateText = StateText(s.State),
                SubText = sub,
                DotBrush = dot,
            };
        }

        private static string StateText(SessionState state) => state switch
        {
            SessionState.Permission => "needs approval",
            SessionState.Question => "waiting",
            SessionState.Thinking => "thinking",
            SessionState.Tool => "working",
            SessionState.Done => "done",
            _ => "idle",
        };
    }
}
