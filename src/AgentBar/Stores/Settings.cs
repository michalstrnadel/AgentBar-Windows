using System.IO;
using System.Text.Json.Nodes;

namespace AgentBar.Stores;

/// Tiny persisted settings bag at ~/.agentbar/settings.json. Best-effort: any read/write
/// failure falls back to defaults.
public static class Settings
{
    private static string FilePath => Path.Combine(Paths.Root, "settings.json");

    /// Opt-in global Ctrl+Alt+A / Ctrl+Alt+D approval shortcut.
    public static bool GlobalApprovalShortcut
    {
        get => GetBool("globalApprovalShortcut");
        set => SetBool("globalApprovalShortcut", value);
    }

    private static bool GetBool(string key)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(FilePath)) is JsonObject o
                && o[key] is JsonValue v && v.TryGetValue(out bool b) && b;
        }
        catch { return false; }
    }

    private static void SetBool(string key, bool value)
    {
        JsonObject root;
        try { root = JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject ?? new JsonObject(); }
        catch { root = new JsonObject(); }
        root[key] = value;
        try
        {
            Directory.CreateDirectory(Paths.Root);
            File.WriteAllText(FilePath, root.ToJsonString());
        }
        catch { /* best effort */ }
    }
}
