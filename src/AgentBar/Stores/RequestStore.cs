using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using AgentBar.Models;

namespace AgentBar.Stores;

/// Watches ~/.agentbar/requests.d/ — one JSON per permission request a blocking hook is
/// currently waiting on. Same folder-is-the-protocol pattern as SessionStore.
public sealed class RequestStore : IDisposable
{
    /// Longest a request can be pending: the hook's default 600s wait plus slack.
    private const double MaxAgeSeconds = 660;

    /// Raised on the UI thread with pending requests, most recent first.
    public event Action<IReadOnlyList<ApprovalRequest>>? Changed;

    public IReadOnlyList<ApprovalRequest> Requests { get; private set; } = Array.Empty<ApprovalRequest>();

    private FileSystemWatcher? _watcher;
    private DispatcherTimer? _timer;
    private List<string> _lastSnapshot = new();

    public void Start()
    {
        Directory.CreateDirectory(Paths.RequestsDir);
        Directory.CreateDirectory(Paths.AnswersDir);

        _watcher = new FileSystemWatcher(Paths.RequestsDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };
        _watcher.Created += OnFsEvent;
        _watcher.Changed += OnFsEvent;
        _watcher.Deleted += OnFsEvent;
        _watcher.Renamed += (s, e) => OnFsEvent(s, e);

        // Fallback poll: dead-hook pruning, maxAge expiry, and orphan-answer GC are
        // time-based and must run without a directory event.
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e) =>
        Application.Current?.Dispatcher.BeginInvoke((Action)Refresh);

    public void Refresh()
    {
        string[] files;
        try { files = Directory.GetFiles(Paths.RequestsDir, "*.json"); }
        catch { return; }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var found = new List<ApprovalRequest>();
        foreach (var file in files)
        {
            var r = ApprovalRequest.FromFile(file);
            if (r is null) continue;
            // Orphans: the waiting hook died (a kill leaves no cleanup), or it expired.
            var watched = r.HookPid > 0 ? r.HookPid : r.Pid;
            var dead = watched > 0 && !ProcessUtil.IsAlive(watched);
            var expired = r.Ts > 0 && now - r.Ts > MaxAgeSeconds;
            if (dead || expired) { TryDelete(file); continue; }
            found.Add(r);
        }

        found = found.OrderByDescending(r => r.Ts).ToList();
        PruneOrphanAnswers(found.Select(r => r.FileName).ToHashSet());

        var snapshot = found.Select(r => r.FileName).ToList();
        if (snapshot.SequenceEqual(_lastSnapshot)) return;
        _lastSnapshot = snapshot;
        Requests = found;
        Changed?.Invoke(found);
    }

    /// Answers nobody consumed (hook died between click and pickup): delete after 60s.
    private static void PruneOrphanAnswers(HashSet<string> liveNames)
    {
        string[] files;
        try { files = Directory.GetFiles(Paths.AnswersDir); }
        catch { return; }

        foreach (var file in files)
        {
            if (liveNames.Contains(Path.GetFileName(file))) continue;
            DateTime modified;
            try { modified = File.GetLastWriteTimeUtc(file); }
            catch { continue; }
            if (DateTime.UtcNow - modified > TimeSpan.FromSeconds(60)) TryDelete(file);
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* another writer/poller raced us */ }
    }

    public void Dispose()
    {
        _timer?.Stop();
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }
    }
}
