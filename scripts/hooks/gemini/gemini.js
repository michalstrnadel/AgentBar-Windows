#!/usr/bin/env node
// AgentBar bridge for Gemini CLI hooks. Maps Gemini's hook events (read from the
// stdin payload's hook_event_name) to a per-session state file in
// ~/.agentbar/state.d/. Observe-only: writes state, emits nothing, exits fast.
// Windows: tasklist liveness + relaunch via the ~/.agentbar/app-path marker.
const fs = require("fs"), os = require("os"), path = require("path"), cp = require("child_process");

const AGENT = "gemini";
const EXEC = "AgentBar.exe";
const agentDir = path.join(os.homedir(), ".agentbar");
const stateDir = path.join(agentDir, "state.d");
const appPathFile = path.join(agentDir, "app-path");

// Gemini event name -> AgentBar state. Exactly the events HookInstaller registers.
const STATE = {
  SessionStart: "idle", SessionEnd: "end",
  BeforeTool: "tool", AfterTool: "thinking",
  BeforeAgent: "thinking", AfterAgent: "done",
};

const safeId = (s) => String(s || "").replace(/[^A-Za-z0-9_.-]/g, "").slice(0, 64) || "unknown";
const running = () => {
  try {
    const out = cp.execSync(`tasklist /FI "IMAGENAME eq ${EXEC}" /NH`,
      { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
    if (out.toLowerCase().includes(EXEC.toLowerCase())) return true;
  } catch {}
  try {
    const w = JSON.parse(fs.readFileSync(path.join(agentDir, "watcher.json"), "utf8"));
    return Date.now() / 1000 - w.ts < 60;
  } catch { return false; }
};
const launch = () => {
  try {
    const exe = fs.readFileSync(appPathFile, "utf8").trim();
    if (exe) cp.spawn(exe, [], { stdio: "ignore", detached: true, windowsHide: true }).unref();
  } catch {}
};
const termProgram = () => process.env.WT_SESSION ? "WindowsTerminal" : (process.env.TERM_PROGRAM || "");
const writeAtomic = (f, o) => { const t = f + "." + process.pid + ".tmp"; fs.writeFileSync(t, JSON.stringify(o)); fs.renameSync(t, f); };

let input = "", done = false;
process.stdin.on("data", (d) => (input += d));
process.stdin.on("end", run);
process.stdin.on("error", run);
setTimeout(run, 1000);

function run() {
  if (done) return; done = true;
  let j = {}; try { j = JSON.parse(input); } catch {}
  const event = j.hook_event_name || "";
  const state = STATE[event];
  if (!state) return process.exit(0);

  const id = j.session_id || j.sessionId || "";
  const cwd = j.cwd || "";
  const statePath = path.join(stateDir, safeId(id) + ".json");

  try { fs.mkdirSync(stateDir, { recursive: true }); } catch {}
  if (state === "end") { try { fs.rmSync(statePath, { force: true }); } catch {} return process.exit(0); }

  const alive = running();
  if (state === "idle" && !alive) {
    try { for (const f of fs.readdirSync(stateDir)) fs.rmSync(path.join(stateDir, f), { force: true }); } catch {}
  }

  let prev = {}; try { prev = JSON.parse(fs.readFileSync(statePath, "utf8")); } catch {}
  try {
    writeAtomic(statePath, {
      ...prev, agent: AGENT, state,
      label: j.tool_name ? String(j.tool_name) : (state === "done" ? "Done" : ""),
      project: cwd ? path.basename(cwd) : "", cwd, sessionId: id,
      entrypoint: "cli", term_program: termProgram(),
      // The shell running Gemini's command string execs the single command, so ppid
      // is the gemini process itself, not a dead intermediate shell.
      pid: process.ppid, started: state !== "idle" ? true : (prev.started || false),
      ts: Math.floor(Date.now() / 1000),
    });
  } catch {}
  if (state === "idle" && !alive) launch();
  process.exit(0);
}
