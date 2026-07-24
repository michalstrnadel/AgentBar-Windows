using System;
using System.IO;

namespace AgentBar.Stores;

/// Filesystem contract shared verbatim with the hook scripts.
/// Mirror of the macOS app's ~/.agentbar layout, rooted at %USERPROFILE%.
public static class Paths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentbar");

    public static string StateDir { get; } = Path.Combine(Root, "state.d");
    public static string RequestsDir { get; } = Path.Combine(Root, "requests.d");
    public static string AnswersDir { get; } = Path.Combine(Root, "answers.d");

    /// Written by the app (and, later, the hook installer) so hooks can relaunch it.
    /// Windows has no LaunchServices/`open -b <bundle>` — this marker is the resolver.
    public static string AppPathMarker { get; } = Path.Combine(Root, "app-path");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(StateDir);
        Directory.CreateDirectory(RequestsDir);
        Directory.CreateDirectory(AnswersDir);
    }
}
