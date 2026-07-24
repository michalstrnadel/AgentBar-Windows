using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace AgentBar.Agents;

/// The known agents and their brand colours. Colours mirror the macOS Agents.swift table.
public static class AgentCatalog
{
    public static readonly IReadOnlyList<Agent> All = new[]
    {
        new Agent { Id = "claude",      Name = "Claude",      Brand = Rgb(0xD9, 0x77, 0x57) },
        new Agent { Id = "codex",       Name = "Codex",       Brand = Rgb(0x10, 0xA3, 0x7F) },
        new Agent { Id = "copilot",     Name = "Copilot",     Brand = Rgb(0x82, 0x50, 0xDF) },
        new Agent { Id = "antigravity", Name = "Antigravity", Brand = Rgb(0x42, 0x85, 0xF4) },
        new Agent { Id = "cursor",      Name = "Cursor",      Brand = Rgb(0x06, 0xB6, 0xD4) },
        new Agent { Id = "gemini",      Name = "Gemini",      Brand = Rgb(0x7C, 0x6B, 0xF5) },
    };

    public static Agent ById(string id) => All.FirstOrDefault(a => a.Id == id) ?? All[0];

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
}
