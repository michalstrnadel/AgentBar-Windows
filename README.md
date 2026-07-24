# AgentBar for Windows

Native Windows notification-area (system tray) app showing live status of AI coding
agents — Claude Code, Codex, Copilot, Cursor, Gemini, Antigravity. Windows counterpart
to the macOS AgentBar: **same `~/.agentbar` state contract and the same Node.js hooks**,
a native tray UI written from scratch in C#/WPF.

> Status: **phase 1 skeleton** — tray icon + live session list. Approval flow and
> keystroke auto-approve land in later phases (see Roadmap).

## How it works

Node.js hook scripts (`scripts/hooks/`) are invoked by each agent and write one JSON
file per session to `%USERPROFILE%\.agentbar\state.d\`. The app watches that folder and
renders the aggregate status in the tray. The folder *is* the protocol — no IPC, no
daemon. Identical to the macOS app; only the native UI layer is rewritten.

## Build & run (Windows only)

Requires the **.NET 8 SDK** on Windows. WPF targets `net8.0-windows`, so this **cannot
be built on macOS/Linux** — only edited there.

```powershell
dotnet build AgentBar.sln -c Release
dotnet run --project src/AgentBar          # dev run
```

Single-file, self-contained publish (~15 MB, no runtime prerequisite):

```powershell
./build.ps1                    # -> publish/AgentBar.exe (+ publish/hooks/)
```

`publish\AgentBar.exe` plus the `publish\hooks\` folder beside it are the whole app. On
first launch it records its own path in `%USERPROFILE%\.agentbar\app-path` so the hooks
can relaunch it, and it **auto-installs the hooks** (see below).

> **Code signing / SmartScreen:** an unsigned exe triggers a SmartScreen warning on
> first run. Sign `AgentBar.exe` with an Authenticode certificate (`signtool`) before
> distributing, or users must click "More info → Run anyway".

## Hooks are installed automatically

On every launch the app copies the bundled hook scripts to `%USERPROFILE%\.agentbar\hooks\`
and wires them into each installed agent's config (idempotent — re-run safe):

- **Claude Code** — `<config>\settings.json`, honoring `CLAUDE_CONFIG_DIR` (plus `~/.claude`).
- **Codex** — appends a `notify` entry to `~/.codex\config.toml` (only if you have none).
- **Cursor** — `~/.cursor\hooks.json` (observational events only).
- **Gemini** — `~/.gemini\settings.json`.

It needs **Node.js on `PATH`** (or at `%ProgramFiles%\nodejs`); without it the Node-based
hooks are skipped. Agents that aren't installed are left untouched.

### Manual wiring (fallback)

If you'd rather wire Claude Code yourself, in `%USERPROFILE%\.claude\settings.json`
(honors `CLAUDE_CONFIG_DIR` if set):

```json
{
  "hooks": {
    "SessionStart": [{ "hooks": [{ "type": "command", "command": "node \"%USERPROFILE%\\path\\to\\scripts\\hooks\\claude\\lifecycle.js\" start" }] }],
    "SessionEnd":   [{ "hooks": [{ "type": "command", "command": "node \"%USERPROFILE%\\path\\to\\scripts\\hooks\\claude\\lifecycle.js\" end" }] }],
    "UserPromptSubmit": [{ "hooks": [{ "type": "command", "command": "node \"...\\update.js\" prompt" }] }],
    "PreToolUse":  [{ "hooks": [{ "type": "command", "command": "node \"...\\update.js\" pre" }] }],
    "PostToolUse": [{ "hooks": [{ "type": "command", "command": "node \"...\\update.js\" post" }] }],
    "Stop":        [{ "hooks": [{ "type": "command", "command": "node \"...\\update.js\" stop" }] }],
    "PermissionRequest": [{ "hooks": [{ "type": "command", "command": "node \"...\\permission.js\"" }] }]
  }
}
```

`PermissionRequest` enables **remote approval** from the tray: the hook blocks while you
click Allow/Deny in the popover, and always falls back to the normal terminal prompt if
the app isn't running or you don't answer in time (`AGENTBAR_APPROVAL_TIMEOUT`, default 600s).

An in-app hook installer (writes these entries for you) is planned; for now wire them
manually. Node.js must be on `PATH`.

## Roadmap

- **Phase 1 (done):** tray icon, `state.d` watcher, popover session list, Claude hooks.
- **Phase 2 (done):** approval flow — `requests.d`/`answers.d`, blocking `permission.js`,
  inline mini-diff/command context, Allow/Always/Deny/Defer buttons.
- **Phase 3 (mostly done):** best-effort keystroke auto-approve (`SendInput` + terminal
  window discovery), opt-in global `Ctrl+Alt+A`/`Ctrl+Alt+D` approval shortcut
  (`RegisterHotKey`), remaining agent hooks (Codex / Cursor / Gemini). **Per-agent
  sprite animation is still deferred** — the tray shows a status dot; porting the macOS
  mascot frames is a separate asset pass.
- **Phase 4 (mostly done):** auto hook-installer (Node-resolved, config-safe), opt-in
  **Start at login** (per-user Run key), single-file publish (`build.ps1`), GitHub-release
  **update check** in the tray menu. Auto-download-and-swap is **deferred** — a running
  `.exe` can't overwrite itself in place, so the menu opens the release page instead; a
  proper updater/installer (MSIX or Inno + side-by-side swap) is the remaining work.

## Relationship to the macOS app

Separate repo, separate codebase. The two share only the on-disk contract (state JSON
schema + hook behavior). Keep the hooks in sync by hand when the contract changes.
