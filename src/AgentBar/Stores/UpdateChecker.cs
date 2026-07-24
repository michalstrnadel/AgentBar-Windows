using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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
    public enum State { Idle, Checking, UpToDate, Available, Downloading, Failed }

    public static readonly UpdateChecker Shared = new();

    public State Status { get; private set; } = State.Idle;
    public string? LatestVersion { get; private set; }
    private string? _downloadUrl;

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
            var root = JsonNode.Parse(body) as JsonObject;
            var tag = root?["tag_name"]?.GetValue<string>();
            if (string.IsNullOrEmpty(tag)) { SetStatus(manual ? State.Failed : State.Idle); return; }

            var latest = tag.StartsWith("v") ? tag[1..] : tag;
            if (IsNewer(latest, CurrentVersion))
            {
                LatestVersion = latest;
                _downloadUrl = PickAsset(root?["assets"] as JsonArray);
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

    /// Download the release zip, stage it, and hand off to a swap script that waits for
    /// this process to exit, copies the new files over the install dir, and relaunches.
    /// A running .exe can't overwrite itself, so the swap must happen from a helper.
    /// Falls back to opening the release page when there's no downloadable asset.
    public async Task InstallAvailable()
    {
        if (Status != State.Available) return;
        if (string.IsNullOrEmpty(_downloadUrl)) { OpenReleasesPage(); return; }

        SetStatus(State.Downloading);
        string? staged = null;
        try
        {
            var work = Path.Combine(Path.GetTempPath(), "agentbar-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            var zipPath = Path.Combine(work, "update.zip");

            using (var resp = await Http.GetAsync(_downloadUrl))
            {
                resp.EnsureSuccessStatusCode();
                await using var fs = File.Create(zipPath);
                await resp.Content.CopyToAsync(fs);
            }

            staged = Path.Combine(work, "staged");
            ZipFile.ExtractToDirectory(zipPath, staged);

            // The archive may wrap the payload in a top-level folder — anchor on the exe.
            var exe = Directory.GetFiles(staged, "AgentBar.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe is null) throw new FileNotFoundException("AgentBar.exe not in the downloaded archive");
            var stagedRoot = Path.GetDirectoryName(exe)!;

            var installDir = Path.GetDirectoryName(Environment.ProcessPath)
                ?? throw new InvalidOperationException("unknown install directory");

            LaunchSwap(stagedRoot, installDir, Environment.ProcessPath!, work);

            Application.Current?.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch
        {
            if (staged is not null) { try { Directory.Delete(Path.GetDirectoryName(staged)!, true); } catch { } }
            SetStatus(State.Failed);
        }
    }

    private static string? PickAsset(JsonArray? assets)
    {
        if (assets is null) return null;
        string? url = null;
        foreach (var a in assets)
        {
            var name = (a as JsonObject)?["name"]?.GetValue<string>() ?? "";
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            url = (a as JsonObject)?["browser_download_url"]?.GetValue<string>();
            if (name.Contains("win", StringComparison.OrdinalIgnoreCase)) break; // prefer a Windows asset
        }
        return url;
    }

    /// Write and launch a detached batch that swaps the files once we've exited.
    /// The batch lives OUTSIDE `work` so it can delete `work` and then itself.
    private static void LaunchSwap(string staged, string installDir, string exe, string work)
    {
        var batch = Path.Combine(Path.GetTempPath(), "agentbar-swap-" + Guid.NewGuid().ToString("N") + ".cmd");
        // `&&` after `find` uses the immediate exit code — avoids the %errorlevel%
        // delayed-expansion trap inside a loop block.
        var script =
            "@echo off\r\n" +
            ":wait\r\n" +
            $"tasklist /FI \"PID eq {Environment.ProcessId}\" /NH 2>nul | find /I \"AgentBar.exe\" >nul && (\r\n" +
            "  timeout /t 1 /nobreak >nul\r\n" +
            "  goto wait\r\n" +
            ")\r\n" +
            $"robocopy \"{staged}\" \"{installDir}\" /E /IS /NFL /NDL /NJH /NJS /NC /NS >nul\r\n" +
            $"start \"\" \"{exe}\"\r\n" +
            $"rmdir /S /Q \"{work}\" >nul 2>&1\r\n" +
            "del \"%~f0\" >nul 2>&1\r\n";
        File.WriteAllText(batch, script);

        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{batch}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
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
