#!/usr/bin/env node
// Regenerate the embedded sprite PNGs from the AgentBar mascot artwork.
// The artwork source stores each mascot as a base64 PNG array in *Frames.swift files;
// this decodes them to PNG files under src/AgentBar/Resources/sprites/ (+ manifest.json).
// Binary data goes file->file on disk.
//
//   node scripts/extract-sprites.js <artwork-source>/Sprites
const fs = require("fs");
const path = require("path");

const SRC = process.argv[2];
if (!SRC) { console.error("usage: node extract-sprites.js <Sprites dir>"); process.exit(1); }
const OUT = path.join(__dirname, "..", "src", "AgentBar", "Resources", "sprites");

const B64 = /"([A-Za-z0-9+/=]{20,})"/g;
const frames = (file) => [...fs.readFileSync(path.join(SRC, file), "utf8").matchAll(B64)].map((m) => m[1]);
const single = (file, name) =>
  (new RegExp(`let\\s+${name}\\s*=\\s*"([A-Za-z0-9+/=]+)"`).exec(fs.readFileSync(path.join(SRC, file), "utf8")) || [])[1];
const writePng = (dir, name, b64) => { fs.mkdirSync(dir, { recursive: true }); fs.writeFileSync(path.join(dir, name), Buffer.from(b64, "base64")); };

const manifest = {};
for (const a of [
  { id: "claude", file: "CrabFrames.swift", fps: 12.5 },
  { id: "codex", file: "CodexFrames.swift", fps: 11 },
  { id: "copilot", file: "CopilotFrames.swift", fps: 11 },
  { id: "antigravity", file: "AntigravityFrames.swift", fps: 11 },
]) {
  const fr = frames(a.file);
  fr.forEach((b64, i) => writePng(path.join(OUT, a.id), String(i).padStart(3, "0") + ".png", b64));
  manifest[a.id] = { frames: fr.length, fps: a.fps };
  console.log(`${a.id}: ${fr.length} frames @ ${a.fps}fps`);
}
for (const m of [{ id: "cursor", name: "cursorLogoPNG" }, { id: "gemini", name: "geminiLogoPNG" }]) {
  const b64 = single("LogoAssets.swift", m.name);
  if (b64) { writePng(path.join(OUT, m.id), "mark.png", b64); manifest[m.id] = { mark: true }; console.log(`${m.id}: mark`); }
}
fs.writeFileSync(path.join(OUT, "manifest.json"), JSON.stringify(manifest, null, 2));
