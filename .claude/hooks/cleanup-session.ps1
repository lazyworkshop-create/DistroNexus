# Cleanup Claude Code session artifacts after each session ends.
# Triggered by the Stop hook in .claude/settings.json.

$artifacts = @(
    'docs/task_plan.md',
    'docs/findings.md',
    'docs/progress.md'
)

foreach ($file in $artifacts) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "[claude-cleanup] Removed $file"
    }
}

# Remove working directory temp files created by Claude Code (tmpclaude-*-cwd)
Get-ChildItem -Path '.' -Filter 'tmpclaude-*' -File | ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "[claude-cleanup] Removed $($_.Name)"
}
