function Resolve-DistroNexusWorkspaceBridge {
    if ($env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH) {
        throw 'DistroNexus WorkspaceBridge path overrides are not supported. Workspace commands fail closed.'
    }
    $packaged = Join-Path $script:ModuleRoot 'WorkspaceBridge\DistroNexus.WorkspaceBridge.dll'
    if (Test-Path -LiteralPath $packaged) { return $packaged }
    $development = Join-Path $script:ProjectRoot 'src\Client\DistroNexus.WorkspaceBridge\bin\Debug\net10.0\DistroNexus.WorkspaceBridge.dll'
    if (Test-Path -LiteralPath $development) { return $development }
    throw 'DistroNexus WorkspaceBridge is required but was not found. Workspace commands fail closed.'
}

function Start-DistroNexusWorkspaceBridge {
    if ($script:WorkspaceBridgeProcess -and -not $script:WorkspaceBridgeProcess.HasExited) { return }
    $path = Resolve-DistroNexusWorkspaceBridge
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.UseShellExecute = $false
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.CreateNoWindow = $true
    $info.FileName = 'dotnet'; $info.Arguments = '"' + $path + '"'
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $info
    if (-not $process.Start()) { throw 'Unable to start DistroNexus WorkspaceBridge.' }
    $script:WorkspaceBridgeProcess = $process
}

function Stop-DistroNexusWorkspaceBridge {
    if ($script:WorkspaceBridgeProcess) {
        try { if (-not $script:WorkspaceBridgeProcess.HasExited) { $script:WorkspaceBridgeProcess.StandardInput.Close(); $script:WorkspaceBridgeProcess.WaitForExit(2000) | Out-Null } }
        finally { $script:WorkspaceBridgeProcess.Dispose(); $script:WorkspaceBridgeProcess = $null }
    }
}

function Invoke-DistroNexusWorkspaceBridge {
    param([Parameter(Mandatory)][string]$Operation, [Guid]$Id, [Guid]$ActionId, $Payload, [Nullable[Int64]]$ExpectedRevision, [string]$Token, [string]$Name)
    Start-DistroNexusWorkspaceBridge
    $request = [ordered]@{ Operation = $Operation; Id = if ($PSBoundParameters.ContainsKey('Id')) { $Id } else { $null }; ActionId = if ($PSBoundParameters.ContainsKey('ActionId')) { $ActionId } else { $null }; Payload = $Payload; ExpectedRevision = if ($PSBoundParameters.ContainsKey('ExpectedRevision')) { $ExpectedRevision } else { $null }; Token = $Token; Name = $Name }
    try {
        $script:WorkspaceBridgeProcess.StandardInput.WriteLine(($request | ConvertTo-Json -Depth 32 -Compress))
        $script:WorkspaceBridgeProcess.StandardInput.Flush()
        do {
            $line = $script:WorkspaceBridgeProcess.StandardOutput.ReadLine()
            if ([string]::IsNullOrWhiteSpace($line)) { throw 'WorkspaceBridge ended without a response.' }
            $response = $line | ConvertFrom-Json
            if ($response.Frame -eq 'progress') {
                $action = if ($response.Value.ActionId) { $response.Value.ActionId } else { 'workspace action' }
                $status = if ($response.Value.Code) { $response.Value.Code } else { 'Running' }
                Write-Progress -Activity 'DistroNexus workspace' -Status ("{0}: {1}" -f $action, $status) -PercentComplete 50
            }
        } while ($response.Frame -eq 'progress')
        Write-Progress -Activity 'DistroNexus workspace' -Completed
    } catch { Stop-DistroNexusWorkspaceBridge; throw }
    if (-not $response.Succeeded) { throw ("{0}: {1}" -f $response.ErrorCode, $response.ErrorMessage) }
    return $response.Value
}
