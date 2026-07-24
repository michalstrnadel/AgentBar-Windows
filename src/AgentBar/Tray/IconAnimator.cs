using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Threading;
using AgentBar.Models;
using AgentBar.Rendering;

namespace AgentBar.Tray;

/// Drives the tray glyph: animates the top session's agent sprite while it's working,
/// otherwise shows the resting frame (with a permission/question dot). Windows analogue
/// of the macOS StatusItemController's render loop, minus the menu-bar text.
public sealed class IconAnimator : IDisposable
{
    private readonly Action<ImageSource> _setIcon;
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<ImageSource> _frames = Array.Empty<ImageSource>();
    private int _index;

    public IconAnimator(Action<ImageSource> setIcon)
    {
        _setIcon = setIcon;
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += (_, _) => Advance();
    }

    public void Update(IReadOnlyList<Session> sessions)
    {
        var top = sessions.Count > 0 ? sessions[0] : null; // already priority-sorted
        var sprite = SpriteCatalog.For(top?.AgentId ?? "claude");
        var state = top?.State ?? SessionState.Idle;
        _frames = sprite.Frames;

        if (state.IsWorking() && _frames.Count > 1)
        {
            _index = 0;
            _timer.Interval = TimeSpan.FromSeconds(1.0 / sprite.Fps);
            _timer.Start();
            _setIcon(IconRenderer.Compose(_frames[0], SessionState.Idle)); // no dot while working
        }
        else
        {
            _timer.Stop();
            var resting = _frames.Count > 0 ? _frames[0] : null;
            _setIcon(IconRenderer.Compose(resting, state));
        }
    }

    private void Advance()
    {
        if (_frames.Count == 0) return;
        _index = (_index + 1) % _frames.Count;
        _setIcon(IconRenderer.Compose(_frames[_index], SessionState.Idle));
    }

    public void Dispose() => _timer.Stop();
}
