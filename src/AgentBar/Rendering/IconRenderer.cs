using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AgentBar.Models;

namespace AgentBar.Rendering;

/// Composites the notification-area glyph: an agent sprite frame scaled into the tray
/// canvas, with a small state dot in the corner for permission/question. Falls back to a
/// plain status dot when an agent has no sprite. (Animation timing lives in IconAnimator.)
public static class IconRenderer
{
    private const int Size = 32; // oversized so the shell downscales crisply across DPIs

    public static ImageSource Compose(ImageSource? frame, SessionState state)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (frame is not null && frame.Width > 0 && frame.Height > 0)
            {
                var scale = Size / Math.Max(frame.Width, frame.Height);
                var w = frame.Width * scale;
                var h = frame.Height * scale;
                dc.DrawImage(frame, new Rect((Size - w) / 2, (Size - h) / 2, w, h));
                DrawCornerDot(dc, state);
            }
            else
            {
                // No sprite: a single status-coloured dot carries the state on its own.
                var center = new Point(Size / 2.0, Size / 2.0);
                var radius = Size * 0.375;
                dc.DrawEllipse(new SolidColorBrush(StatusColors.For(state)), null, center, radius, radius);
            }
        }
        var rtb = new RenderTargetBitmap(Size, Size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    /// Amber "awaiting permission" / yellow "question" badge, top-right. Nothing for the
    /// working and resting states — the sprite itself carries those.
    private static void DrawCornerDot(DrawingContext dc, SessionState state)
    {
        if (state is not (SessionState.Permission or SessionState.Question)) return;
        var d = Size * 0.36;
        var fill = new SolidColorBrush(StatusColors.For(state));
        var ring = new Pen(new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)), 0.75);
        dc.DrawEllipse(fill, ring, new Point(Size - d / 2, d / 2), d / 2, d / 2);
    }
}

/// Shared status palette so the tray glyph and the popover dots never drift apart.
public static class StatusColors
{
    public static readonly Color Idle = Color.FromRgb(0x8E, 0x8E, 0x93);

    public static Color For(SessionState state) => state switch
    {
        SessionState.Permission => Color.FromRgb(0xFF, 0x9F, 0x0A),                     // amber: needs you
        SessionState.Question => Color.FromRgb(0xFF, 0xD6, 0x0A),                       // yellow: waiting
        SessionState.Thinking or SessionState.Tool => Color.FromRgb(0x30, 0xD1, 0x58), // green: working
        _ => Idle,
    };
}
