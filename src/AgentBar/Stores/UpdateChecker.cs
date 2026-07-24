using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AgentBar.Stores;

/// Update awareness from GitHub Releases — no windows, no daemons. Phase 4 does the
/// CHECK and points the user at the release page; it does not auto-download-and-swap
/// (a running .exe can't overwrite itself without a separate updater — deferred).
public sealed class UpdateChecker
{
    public enum State { Idle, Checking, UpToDate, Available, Failed }

    public static readonly UpdateChecker Shared = new();

    public State Status { get; private set; } = State.Idle;
    public string? LatestVersion { get; private set; }

    /// Fired on the UI thread whenever Status changes.
    public event Action? Changed;

    // Release source. Placeholder until the repo is published under this owner.
    private const string Repo = "michalstrnadel/AgentBar-Windows";
    private static readonly HttpClient Http = CreateClient();
    private DispatcherTimer? _timer;

    public string CurrentVersion =>
        Environment.GetEnvironmentVariable("AGENTBAR_VERSION_OVERRIDE")
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "0";

    /// First check shortly after launch (network may still be waking), then daily.
    public void StartPeriodicChecks()
    {
        var initial = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        initial.Tick += (_, _) => { initial.Stop(); _ = Check(manual: false); };
        initial.Start();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(24) };
        _timer.Tick += (_, _) => _ = Check(manual: false);
        _timer.Start();
    }

    public async Task Check(bool manual)
    {
        if (Status == State.Checking) return;
        if (Status == State.Available && !manual) return; // keep the offer visible
        if (manual) SetStatus(State.Checking);

        try
        {
            using var resp = await Http.GetAsync($"https://api.github.com/repos/{Repo}/releases/latest");
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync();
            var tag = (JsonNode.Parse(body) as JsonObject)?["tag_name"]?.GetValue<string>();
            if (string.IsNullOrEmpty(tag)) { SetStatus(manual ? State.Failed : State.Idle); return; }

            var latest = tag.StartsWith("v") ? tag[1..] : tag;
            if (IsNewer(latest, CurrentVersion))
            {
                LatestVersion = latest;
                SetStatus(State.Available);
            }
            else
            {
                SetStatus(manual ? State.UpToDate : State.Idle);
            }
        }
        catch
        {
            // Offline on a laptop isn't an error worth shouting about when automatic.
            SetStatus(manual ? State.Failed : State.Idle);
        }
    }

    /// "Up to date" / "failed" are moment-in-time answers; forget them so the menu
    /// shows a fresh "Check for Updates…" next open.
    public void ClearTransient()
    {
        if (Status is State.UpToDate or State.Failed) SetStatus(State.Idle);
    }

    public void OpenReleasesPage()
    {
        var url = LatestVersion is not null
            ? $"https://github.com/{Repo}/releases/tag/v{LatestVersion}"
            : $"https://github.com/{Repo}/releases/latest";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* no browser / blocked */ }
    }

    /// Numeric semver compare, tolerant of stray suffixes ("1.6.0-beta" → 1.6.0).
    public static bool IsNewer(string a, string b)
    {
        static int[] Nums(string s) => s.Split('.')
            .Select(part => int.TryParse(new string(part.TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0)
            .ToArray();

        var x = Nums(a);
        var y = Nums(b);
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var l = i < x.Length ? x[i] : 0;
            var r = i < y.Length ? y[i] : 0;
            if (l != r) return l > r;
        }
        return false;
    }

    private void SetStatus(State state)
    {
        if (state == Status) return;
        Status = state;
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) Changed?.Invoke();
        else dispatcher.Invoke(() => Changed?.Invoke());
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AgentBar");     // GitHub rejects UA-less requests
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
