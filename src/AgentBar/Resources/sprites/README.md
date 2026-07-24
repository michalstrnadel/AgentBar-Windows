# Sprites

Per-agent tray animation frames (and single marks for Cursor/Gemini), as PNGs embedded
in the assembly via `<Resource>` and loaded by `SpriteCatalog`.

- `claude/` 20 frames @ 12.5fps · `codex/` 14 @ 11 · `copilot/` 16 @ 11 · `antigravity/` 16 @ 11
- `cursor/mark.png`, `gemini/mark.png` — single marks, tinted with the brand colour at runtime.

These are generated from the AgentBar macOS app's artwork. To regenerate, point
`scripts/extract-sprites.js` at the macOS `Sources/AgentBar/Sprites` directory and run it
with Node; it decodes the base64 frame arrays into the PNGs here plus `manifest.json`.
Frame counts/fps are mirrored in `SpriteCatalog`.
