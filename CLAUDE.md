# AgentBar for Windows — AI Instructions

Context for AI coding assistants working on this repository.

## Project
Native Windows notification-area (system tray) app showing live status of AI coding
agents (Claude Code, Codex, Copilot, Cursor, Gemini, Antigravity). C#/.NET 8 + WPF,
tray via `Hardcodet.NotifyIcon.Wpf`. Node.js hook scripts in `scripts/hooks/` write
per-session JSON to `%USERPROFILE%\.agentbar\state.d\`; the app watches that folder.

The `~/.agentbar` state/approval file layout (state JSON schema + hook behavior) is a
stable on-disk contract the hooks and the app agree on — keep it stable when editing hooks.

## Build & run (Windows only)
```powershell
dotnet build AgentBar.sln -c Release
dotnet run --project src/AgentBar
```
WPF targets `net8.0-windows` — builds and runs on Windows only.

## Rules
1. One file, one responsibility. Don't grow a god-object controller.
2. Tray only: no main window (the popover is borderless/transient), no taskbar button,
   no heavy dependencies. `Hardcodet.NotifyIcon.Wpf` is the only third-party package.
3. Hooks must never block the host agent: async, atomic writes (`tmp` + rename), exit
   fast. Sole exception: `permission.js` blocks while the session already waits on the
   human, and must always time out silently to the normal terminal prompt.
4. Keep the `~/.agentbar` JSON contract stable — the hooks and the app must agree on it.
5. Adding an agent: entry in `AgentCatalog.All`, artwork, optional hook dir under
   `scripts/hooks/<agent>/`. Nothing else should need touching.
6. There's no launch-by-identity on Windows: the app writes `~/.agentbar/app-path` at
   startup and hooks relaunch it from there; liveness is `tasklist`.

## Layout
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
- `Rendering/IconRenderer.cs` — composites a sprite frame + state dot; status palette.
- `Rendering/SpriteCatalog.cs` — loads embedded per-agent frames / tinted marks.
- `Tray/IconAnimator.cs` — cycles the top agent's sprite while working (resting otherwise).
- `Tray/` — `TrayController` (NotifyIcon), `PopoverWindow` (sessions + approval cards),
  `TrayInterop` (multi-monitor placement), `RelayCommand`.
- `Input/` — `KeystrokeApprover` + `Keyboard` (SendInput) + `TerminalWindow` (find/focus
  the agent's terminal via process ancestry), `HotKeyCenter` (global RegisterHotKey).
- `Stores/Settings.cs` — persisted flags (`~/.agentbar/settings.json`).
- `Stores/HookInstaller.cs` — idempotent copy of bundled hooks + wiring into each agent's
  config (Claude/Codex/Cursor/Gemini); resolves node, never clobbers unparseable configs.
- `Stores/UpdateChecker.cs` — GitHub-release check (no auto-swap; opens the release page).
- `Stores/Autostart.cs` — per-user Run-key start-at-login toggle.
