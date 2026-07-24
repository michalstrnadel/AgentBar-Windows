# Copilot

GitHub Copilot CLI has no state hooks, so there is no live-status bridge here. AgentBar
surfaces a Copilot session only when another signal marks it, and offers **best-effort
keystroke approval** from the popover: it brings the terminal forward and sends `y` then
`Enter` (see `AgentCatalog` `ApproveKeys`). Delivery to the right tab is not guaranteed.
