using System.Windows.Media;

namespace AgentBar.Agents;

/// Everything AgentBar knows about one AI coding agent.
/// Adding an agent = one entry in AgentCatalog.All (+ artwork, once sprites land).
public sealed class Agent
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Color Brand { get; init; }
}
