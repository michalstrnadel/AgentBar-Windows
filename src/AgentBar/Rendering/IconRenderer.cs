using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AgentBar.Models;

namespace AgentBar.Rendering;

/// Renders the notification-area glyph. Phase 1: a status dot coloured by the most
/// urgent session. (Per-agent sprite animation from the macOS app lands in a later phase.)
public static class IconRenderer
{
    // Render oversized (32px) so the shell can downscale crisply: it asks for 16px at
    // 100%, up to 32px at 200%. A single 32px source covers the whole DPI range.
    private const int Size = 32;

    public static ImageSource Render(IReadOnlyList<Session> sessions)
    {
        var color = AggregateColor(sessions);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var center = new Point(Size / 2.0, Size / 2.0);
            var radius = Size * 0.375; // 12px at 32px canvas — same proportions as before
            dc.DrawEllipse(new SolidColorBrush(color), null, center, radius, radius);
        }
        var rtb = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private static Color AggregateColor(IReadOnlyList<Session> sessions)
    {
        if (sessions.Count == 0) return StatusColors.Idle;
        return StatusColors.For(sessions[0].State); // already priority-sorted
    }
}

/// Shared status palette so the tray glyph and the popover dots never drift apart.
public static class StatusColors
{
    public static readonly Color Idle = Color.FromRgb(0x8E, 0x8E, 0x93);

    public static Color For(SessionState state) => state switch
    {
        SessionState.Permission => Color.FromRgb(0xFF, 0x9F, 0x0A),                 // amber: needs you
        SessionState.Question => Color.FromRgb(0xFF, 0xD6, 0x0A),                   // yellow: waiting
        SessionState.Thinking or SessionState.Tool => Color.FromRgb(0x30, 0xD1, 0x58), // green: working
        _ => Idle,
    };
}
