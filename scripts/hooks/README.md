# Hooks (Windows)

Windows-adapted copies of the macOS AgentBar hooks. Same `~/.agentbar` JSON contract;
the only differences are OS-specific:

- **Liveness:** `tasklist /FI "IMAGENAME eq AgentBar.exe"` instead of `pgrep -x AgentBar`.
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
  app-running probe uses `tasklist` (not `pgrep`), and it handles `SIGINT`/`SIGBREAK`
  (Windows doesn't deliver `SIGTERM`) so a killed hook still cleans up its request file.

## Not yet ported (later phases)
- `codex/`, `cursor/`, `gemini/`, `copilot/`, `antigravity/` — phase 3.

Keep these in sync with the macOS repo whenever the state/approval contract changes.
