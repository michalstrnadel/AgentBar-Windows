using System;
using System.IO;
using System.Text.Json;
using AgentBar.Models;

namespace AgentBar.Stores;

/// Writes the user's decision for a pending request; the blocking hook polls for it.
/// The answer file name must equal the request's so the hook finds its own answer.
public static class AnswerWriter
{
    /// behavior: "allow" | "always" | "deny" | "defer". The hook maps these to the
    /// PermissionRequest decision schema; "defer" means fall back to the terminal prompt.
    /// ruleRawJson (for "always") is Claude's suggestion echoed back verbatim — the hook
    /// only pins a rule when it structurally matches something Claude itself offered.
    public static void Write(string behavior, string? ruleRawJson, ApprovalRequest request)
    {
        try { Directory.CreateDirectory(Paths.AnswersDir); } catch { return; }

        var behaviorJson = JsonSerializer.Serialize(behavior);
        var json = ruleRawJson is null
            ? $"{{\"behavior\":{behaviorJson}}}"
            : $"{{\"behavior\":{behaviorJson},\"rule\":{ruleRawJson}}}";

        var final = Path.Combine(Paths.AnswersDir, request.FileName);
        var tmp = final + "." + Environment.ProcessId + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, final, overwrite: true); // rename: atomic for the hook's poller
        }
        catch
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
        }
    }
}
