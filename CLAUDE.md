# AgentBar for Windows — AI Instructions

Context for AI coding assistants working on this repository.

## Project
Native Windows notification-area (system tray) app showing live status of AI coding
agents (Claude Code, Codex, Copilot, Cursor, Gemini, Antigravity). C#/.NET 8 + WPF,
tray via `Hardcodet.NotifyIcon.Wpf`. Node.js hook scripts in `scripts/hooks/` write
per-session JSON to `%USERPROFILE%\.agentbar\state.d\`; the app watches that folder.

Windows counterpart of the macOS AgentBar. The two repos share **only the on-disk
contract** (state JSON schema + hook behavior), not code. The state/approval file
layout under `~/.agentbar` is identical across platforms.

## Build & run (Windows only)
```powershell
dotnet build AgentBar.sln -c Release
dotnet run --project src/AgentBar
```
WPF targets `net8.0-windows` — **cannot build on macOS/Linux**, only edit there.

## Rules
1. One file, one responsibility. Don't grow a god-object controller.
2. Tray only: no main window (the popover is borderless/transient), no taskbar button,
   no heavy dependencies. `Hardcodet.NotifyIcon.Wpf` is the only third-party package.
3. Hooks must never block the host agent: async, atomic writes (`tmp` + rename), exit
   fast. Sole exception: `permission.js` blocks while the session already waits on the
   human, and must always time out silently to the normal terminal prompt.
4. Keep the `~/.agentbar` contract identical to the macOS app. Changing the JSON shape
   means changing both repos' hooks in lockstep.
5. Adding an agent: entry in `AgentCatalog.All`, artwork, optional hook dir under
   `scripts/hooks/<agent>/`. Nothing else should need touching.
6. Windows has no LaunchServices: the app writes `~/.agentbar/app-path` at startup and
   hooks relaunch it from there; liveness is `tasklist`, not `pgrep`.

## Layout (mirrors the macOS unit split)
- `Stores/Paths.cs` — the `~/.agentbar` path contract.
- `Stores/ProcessUtil.cs` — shared pid liveness probe (`tasklist`-equivalent).
- `Models/` — `Session`, `SessionState`, `ApprovalRequest` + `ApprovalContext`
  (POCO + tolerant JSON parse).
- `Agents/` — agent catalog + brand colours.
- `Stores/SessionStore.cs` — `state.d` watcher (`FileSystemWatcher` + 2s poll, prune, diff).
- `Stores/RequestStore.cs` — `requests.d` watcher; prunes dead-hook / expired requests
  and orphan answers.
- `Stores/AnswerWriter.cs` — atomic decision write to `answers.d` (echoes Claude's rule
  suggestion verbatim on "always").
- `Rendering/IconRenderer.cs` — tray glyph + shared status palette.
- `Tray/` — `TrayController` (NotifyIcon), `PopoverWindow` (sessions + approval cards),
  `TrayInterop` (multi-monitor placement), `RelayCommand`.
