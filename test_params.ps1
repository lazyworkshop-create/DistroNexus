Import-Module 'd:\wsl\DistroNexus\release\DistroNexus-v2.0.0-Release\PowerShell\DistroNexus.psd1' -Force

$cmd = Get-Command Install-DistroNexusInstance

Write-Host "`nParameter Sets:" -ForegroundColor Cyan
foreach ($ps in $cmd.ParameterSets) {
    Write-Host "  $($ps.Name)" -ForegroundColor Yellow
    Write-Host "  Parameters in this set:" -ForegroundColor Gray
    
    $ps.Parameters | Where-Object { $_.Name -notin @('Verbose','Debug','ErrorAction','WarningAction','InformationAction','ErrorVariable','WarningVariable','InformationVariable','OutVariable','OutBuffer','PipelineVariable','WhatIf','Confirm','ProgressAction') } | 
        Select-Object Name, IsMandatory, @{Name='ParamSet';Expression={$_.ParameterSetName}} | 
        Format-Table -AutoSize
}

Write-Host "`nTesting command:" -ForegroundColor Cyan
Write-Host "Install-DistroNexusInstance -DistroName 'Ubuntu 24.04 LTS' -InstallPath 'D:\test' -InstanceName 'test-ubuntu' -WhatIf" -ForegroundColor White

try {
    Install-DistroNexusInstance -DistroName 'Ubuntu 24.04 LTS' -InstallPath 'D:\test' -InstanceName 'test-ubuntu' -WhatIf
} catch {
    Write-Host "`nError: $_" -ForegroundColor Red
}
