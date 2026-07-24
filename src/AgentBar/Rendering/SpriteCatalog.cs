using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AgentBar.Agents;

namespace AgentBar.Rendering;

/// A decoded, ready-to-draw sprite for one agent: its animation frames and playback rate.
public sealed class Sprite
{
    public IReadOnlyList<ImageSource> Frames { get; init; } = Array.Empty<ImageSource>();
    public double Fps { get; init; } = 11;
    public bool IsEmpty => Frames.Count == 0;
}

/// Loads per-agent sprites from the embedded PNGs under Resources/sprites/.
/// Frame counts/fps come from the extraction (scripts/); single-mark agents are tinted
/// with their brand colour. Everything is decoded once and cached.
public static class SpriteCatalog
{
    // Frame counts + fps as extracted from the mascot artwork.
    private static readonly (string Id, int Frames, double Fps)[] Framed =
    {
        ("claude", 20, 12.5),
        ("codex", 14, 11),
        ("copilot", 16, 11),
        ("antigravity", 16, 11),
    };
    private static readonly string[] Marked = { "cursor", "gemini" };

    private static readonly Dictionary<string, Sprite> Cache = new();

    public static Sprite For(string agentId)
    {
        if (Cache.TryGetValue(agentId, out var cached)) return cached;
        var sprite = Build(agentId);
        Cache[agentId] = sprite;
        return sprite;
    }

    private static Sprite Build(string agentId)
    {
        foreach (var (id, count, fps) in Framed)
        {
            if (id != agentId) continue;
            var frames = new List<ImageSource>(count);
            for (var i = 0; i < count; i++)
            {
                var img = Load($"{id}/{i:000}.png");
                if (img is not null) frames.Add(img);
            }
            return new Sprite { Frames = frames, Fps = fps };
        }

        foreach (var id in Marked)
        {
            if (id != agentId) continue;
            var mark = Load($"{id}/mark.png");
            if (mark is null) break;
            var tinted = Tint(mark, AgentCatalog.ById(id).Brand);
            return new Sprite { Frames = new[] { tinted }, Fps = 1 };
        }

        return new Sprite(); // empty -> IconRenderer falls back to a status dot
    }

    private static ImageSource? Load(string relative)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Resources/sprites/{relative}", UriKind.Absolute);
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = uri;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch { return null; }
    }

    /// Recolour every pixel to the brand colour, keeping the source alpha — turns a
    /// monochrome mark into a brand-tinted glyph.
    private static ImageSource Tint(ImageSource source, Color brand)
    {
        var bmp = new FormatConvertedBitmap((BitmapSource)source, PixelFormats.Bgra32, null, 0);
        var w = bmp.PixelWidth;
        var h = bmp.PixelHeight;
        var stride = w * 4;
        var px = new byte[h * stride];
        bmp.CopyPixels(px, stride, 0);

        for (var i = 0; i < px.Length; i += 4)
        {
            px[i] = brand.B;
            px[i + 1] = brand.G;
            px[i + 2] = brand.R;
            // px[i + 3] (alpha) preserved
        }

        var outBmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        outBmp.WritePixels(new System.Windows.Int32Rect(0, 0, w, h), px, stride, 0);
        outBmp.Freeze();
        return outBmp;
    }
}
