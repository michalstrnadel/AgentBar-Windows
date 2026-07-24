using System.Windows.Media;

namespace AgentBar.Agents;

/// Everything AgentBar knows about one AI coding agent.
/// Adding an agent = one entry in AgentCatalog.All (+ artwork, once sprites land).
public sealed class Agent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Color Brand { get; init; }

    /// Virtual-key codes sent to approve a prompt in the agent's own terminal UI, for
    /// agents with no decision hook. null = keystroke approval not applicable (Claude has
    /// the native hook path; Cursor/Gemini are observe-only; Antigravity is an IDE).
    public ushort[]? ApproveKeys { get; init; }
}
