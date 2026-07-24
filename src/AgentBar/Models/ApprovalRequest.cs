using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AgentBar.Models;

/// What a permission request will do, in enough detail to render inline without the terminal.
public abstract record ApprovalContext
{
    public sealed record Bash(string Command) : ApprovalContext;
    public sealed record Diff(string Old, string New, int More) : ApprovalContext;
    public sealed record Write(string Preview) : ApprovalContext;
}

/// One pending permission request, decoded from ~/.agentbar/requests.d/*.json (written by
/// the blocking permission hook while it waits for the user's answer). The file name is the
/// shared key: the answer file must reuse it so the hook finds its own answer.
public sealed class ApprovalRequest
{
    public string FileName { get; init; } = "";
    public string SessionId { get; init; } = "";
    public string AgentId { get; init; } = "claude";
    public string ToolName { get; init; } = "";
    public string Display { get; init; } = "";
    public string ToolInputPretty { get; init; } = "";

    /// Raw JSON of Claude's rule suggestion, echoed back verbatim on "Always allow" so the
    /// hook's canonical-equality check accepts it. Null when Claude offered no rule.
    public string? RuleSuggestionRaw { get; init; }

    public ApprovalContext? Context { get; init; }
    public int Pid { get; init; }      // the waiting hook's parent (the claude process)
    public int HookPid { get; init; }  // the waiting hook itself; primary liveness handle
    public double Ts { get; init; }

    public static ApprovalRequest? FromFile(string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var o = doc.RootElement;
            return new ApprovalRequest
            {
                FileName = Path.GetFileName(filePath),
                SessionId = Str(o, "sessionId", ""),
                AgentId = Str(o, "agent", "claude"),
                ToolName = Str(o, "toolName", ""),
                Display = Str(o, "display", Str(o, "toolName", "request")),
                ToolInputPretty = Str(o, "toolInputPretty", ""),
                RuleSuggestionRaw = RawOrNull(o, "ruleSuggestion"),
                Context = DecodeContext(o),
                Pid = Int(o, "pid", 0),
                HookPid = Int(o, "hookPid", 0),
                Ts = Dbl(o, "ts", 0),
            };
        }
        catch { return null; }
    }

    /// Text of the rule "Always allow" would persist. Claude Code suggestions come as
    /// {rules:[{toolName, ruleContent}]}; render those as "Bash(git push:*)" not raw JSON.
    public string? RuleDescription
    {
        get
        {
            if (RuleSuggestionRaw is null) return null;
            try
            {
                using var doc = JsonDocument.Parse(RuleSuggestionRaw);
                var r = doc.RootElement;
                if (r.ValueKind == JsonValueKind.Object)
                {
                    if (r.TryGetProperty("rule", out var rule) && rule.ValueKind == JsonValueKind.String)
                        return rule.GetString();

                    if (r.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array)
                    {
                        var parts = new List<string>();
                        foreach (var item in rules.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;
                            if (!item.TryGetProperty("ruleContent", out var rc) || rc.ValueKind != JsonValueKind.String) continue;
                            var content = rc.GetString() ?? "";
                            var tool = item.TryGetProperty("toolName", out var tn) && tn.ValueKind == JsonValueKind.String
                                ? tn.GetString() ?? "" : "";
                            parts.Add(string.IsNullOrEmpty(tool) ? content : $"{tool}({content})");
                        }
                        if (parts.Count > 0) return string.Join(", ", parts);
                    }
                }
            }
            catch { /* fall through to raw */ }
            return RuleSuggestionRaw;
        }
    }

    /// RuleDescription cut to button/tooltip length.
    public string? RuleMenuTitle
    {
        get
        {
            var d = RuleDescription;
            if (d is null) return null;
            return d.Length > 48 ? d[..47] + "…" : d;
        }
    }

    private static ApprovalContext? DecodeContext(JsonElement o)
    {
        if (!o.TryGetProperty("context", out var c) || c.ValueKind != JsonValueKind.Object) return null;
        if (!c.TryGetProperty("kind", out var k) || k.ValueKind != JsonValueKind.String) return null;
        return k.GetString() switch
        {
            "bash" => new ApprovalContext.Bash(Str(c, "command", "")),
            "diff" => new ApprovalContext.Diff(Str(c, "old", ""), Str(c, "new", ""), Int(c, "more", 0)),
            "write" => new ApprovalContext.Write(Str(c, "preview", "")),
            _ => null,
        };
    }

    private static string? RawOrNull(JsonElement o, string k) =>
        o.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetRawText() : null;

    private static string Str(JsonElement o, string k, string def) =>
        o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? def) : def;

    private static int Int(JsonElement o, string k, int def) =>
        o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : def;

    private static double Dbl(JsonElement o, string k, double def) =>
        o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var n) ? n : def;
}
