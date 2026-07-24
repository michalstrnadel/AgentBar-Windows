# Hooks (Windows)

The AgentBar hook scripts, Windows-adapted. Same `~/.agentbar` JSON contract; the
differences are OS-specific:

- **Liveness:** `tasklist /FI "IMAGENAME eq AgentBar.exe"`.
- **Relaunch:** read the app path from `%USERPROFILE%\.agentbar\app-path` (written by the
  app on first run) and spawn it, instead of `open -b <bundle-id>`. Idempotent — only
  launches when no instance is running.
- **Terminal:** `WT_SESSION` -> `"WindowsTerminal"`, else `TERM_PROGRAM` (usually empty).

All writes stay atomic (`<file>.<pid>.tmp` + rename) and every hook exits fast so it
never blocks the host agent.

## Ported so far
- `claude/lifecycle.js` — SessionStart / SessionEnd.
- `claude/update.js` — prompt / pre / post / stop state.
- `claude/permission.js` — blocking PermissionRequest approval hook. Windows delta:
  app-running probe uses `tasklist`, and it handles `SIGINT`/`SIGBREAK`
  (Windows doesn't deliver `SIGTERM`) so a killed hook still cleans up its request file.
- `codex/notify.js` — Codex notify adapter (`notify = ["node", "<path>"]` in
  `%USERPROFILE%\.codex\config.toml`). Completion-only; no live "working" signal.
- `cursor/cursor.js`, `gemini/gemini.js` — CLI hook bridges. Windows delta: `tasklist`
  liveness + relaunch via the `~/.agentbar/app-path` marker.

## Not hook-driven
- `copilot/`, `antigravity/` — README only. Copilot uses best-effort keystroke approval
  from the popover; Antigravity is an IDE (see each dir's README).

Keep these in sync whenever the `~/.agentbar` state/approval contract changes.
