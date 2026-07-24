#!/usr/bin/env pwsh
# Publish AgentBar as a single self-contained .exe (no .NET runtime prerequisite).
# Windows only — WPF targets net8.0-windows and cannot build on macOS/Linux.
#
#   ./build.ps1                 # Release, win-x64 -> publish/AgentBar.exe
#   ./build.ps1 -Runtime win-arm64
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSCommandPath

dotnet publish "$root/src/AgentBar" `
    -c $Configuration -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -o "$root/publish"

# Release asset the in-app updater downloads (AgentBar.exe at the root + hooks/).
$zip = "$root/publish/AgentBar-$Runtime.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$root/publish/AgentBar.exe", "$root/publish/hooks" -DestinationPath $zip

Write-Host "Published: $root/publish/AgentBar.exe"
Write-Host "Release zip: $zip  (attach to the GitHub release for auto-update)"
Write-Host "The hook scripts sit beside the exe under publish/hooks/ and are copied to"
Write-Host "%USERPROFILE%\.agentbar\hooks on first launch. Keep them beside the exe."
