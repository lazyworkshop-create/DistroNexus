# Cleanup Claude Code session artifacts after each session ends.
# Triggered by the Stop hook in .claude/settings.json.

# Remove working directory temp files created by Claude Code (tmpclaude-*-cwd)
Get-ChildItem -Path '.' -Filter 'tmpclaude-*' -File | ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "[claude-cleanup] Removed $($_.Name)"
}
