Describe 'Workspace commands use the Core WorkspaceBridge' {
  BeforeAll { Import-Module "$PSScriptRoot/../../../../src/PowerShell/DistroNexus.psd1" -Force }
  BeforeEach { $env:DISTRONEXUS_WORKSPACE_STORE_ROOT = Join-Path $TestDrive ([guid]::NewGuid().ToString()) }
  AfterEach { Remove-Item Env:DISTRONEXUS_WORKSPACE_STORE_ROOT -ErrorAction SilentlyContinue }

  It 'uses the persistent packaged protocol for create, update, duplicate, export and remove' {
    $item = New-DistroNexusWorkspace -Name demo -Instance Ubuntu -ExpectedRevision 0
    $item.Revision | Should -Be 1
    $changed = Set-DistroNexusWorkspace -Id $item.Id -Name changed -ExpectedRevision 1
    $changed.DisplayName | Should -Be 'changed'
    $duplicate = Copy-DistroNexusWorkspace -Id $item.Id -Name duplicate -ExpectedRevision 2
    $out = Join-Path $TestDrive 'workspace.json'; Export-DistroNexusWorkspace -Id $item.Id -Path $out -ExpectedRevision 2
    (Get-Content -LiteralPath $out -Raw | ConvertFrom-Json).DisplayName | Should -Be 'changed'
    Remove-DistroNexusWorkspace -Id $item.Id -ExpectedRevision 2 -Confirm:$false
    Remove-DistroNexusWorkspace -Id $duplicate.Id -ExpectedRevision 1 -Confirm:$false
    @(Get-DistroNexusWorkspace).Count | Should -Be 0
  }

  It 'rejects a stale revision before writing an export file' {
    $item = New-DistroNexusWorkspace -Name export-revision -Instance Ubuntu -ExpectedRevision 0
    Set-DistroNexusWorkspace -Id $item.Id -Name changed -ExpectedRevision 1 | Out-Null
    $out = Join-Path $TestDrive 'stale-workspace.json'

    { Export-DistroNexusWorkspace -Id $item.Id -Path $out -ExpectedRevision 1 } | Should -Throw '*Conflict*'
    Test-Path -LiteralPath $out | Should -BeFalse
    Export-DistroNexusWorkspace -Id $item.Id -Path $out -ExpectedRevision 2
    (Get-Content -LiteralPath $out -Raw | ConvertFrom-Json).DisplayName | Should -Be 'changed'
  }

  It 'preserves a Core import token and enforces optimistic revisions' {
    $path = Join-Path $TestDrive 'import.json'
    @{ Id=[guid]::NewGuid(); DisplayName='import'; InstanceName='Ubuntu'; PreflightChecks=@(); ActionGroups=@(); ClosePolicy=@{Mode='None';ServiceNames=@()}; TrustState='Trusted'; MissingInstanceRemediation='BlockWithGuidance' } | ConvertTo-Json -Depth 8 | Set-Content $path
    $preview = Get-DistroNexusWorkspaceImportPreview -Path $path
    $item = Import-DistroNexusWorkspace -Path $path -ImportToken $preview.ImportToken -ExpectedRevision 0
    $item.TrustState | Should -Be 'Untrusted'
    { Approve-DistroNexusWorkspaceTrust -Id $item.Id -ExpectedRevision 0 -Confirm:$false } | Should -Throw '*Conflict*'
    (Approve-DistroNexusWorkspaceTrust -Id $item.Id -ExpectedRevision 1 -Confirm:$false).TrustState | Should -Be 'Trusted'
    { Import-DistroNexusWorkspace -Path $path -ImportToken $preview.ImportToken -ExpectedRevision 0 } | Should -Throw '*preview*'
  }

  It 'requires import creation revision zero and rejects a stale preview' {
    $path = Join-Path $TestDrive 'import.json'
    @{ Id=[guid]::NewGuid(); DisplayName='import'; InstanceName='Ubuntu'; PreflightChecks=@(); ActionGroups=@(); ClosePolicy=@{Mode='None';ServiceNames=@()}; TrustState='Trusted'; MissingInstanceRemediation='BlockWithGuidance' } | ConvertTo-Json -Depth 8 | Set-Content $path
    $preview = Get-DistroNexusWorkspaceImportPreview -Path $path
    { Import-DistroNexusWorkspace -Path $path -ImportToken $preview.ImportToken -ExpectedRevision 1 } | Should -Throw '*expected revision must be zero*'
    New-DistroNexusWorkspace -Name competing -Instance Ubuntu -ExpectedRevision 0 | Out-Null
    { Import-DistroNexusWorkspace -Path $path -ImportToken $preview.ImportToken -ExpectedRevision 0 } | Should -Throw '*stale*'
  }

  It 'does not mutate Bridge state under WhatIf' {
    $before = @(Get-DistroNexusWorkspace).Count
    New-DistroNexusWorkspace -Name test -Instance Ubuntu -ExpectedRevision 0 -WhatIf
    @(Get-DistroNexusWorkspace).Count | Should -Be $before
  }

  It 'routes every workspace mutator through a token-free Core dry-run under WhatIf' {
    $id = [guid]'11111111-1111-1111-1111-111111111111'; $actionId = [guid]::NewGuid()
    $path = Join-Path $TestDrive 'whatif-import.json'
    @{ Id=$id; DisplayName='dry'; InstanceName='Ubuntu'; PreflightChecks=@(); ActionGroups=@(); ClosePolicy=@{Mode='None';ServiceNames=@()}; TrustState='Trusted'; MissingInstanceRemediation='BlockWithGuidance' } | ConvertTo-Json -Depth 8 | Set-Content $path
    InModuleScope DistroNexus {
      $script:workspaceBridgeRequests = @()
      Mock Invoke-DistroNexusWorkspaceBridge {
        param($Operation, $Id, $ActionId, $Payload, $ExpectedRevision, $Token, $Name)
        $script:workspaceBridgeRequests += [pscustomobject]@{ Operation=$Operation; Token=$Token }
        if ($Operation -eq 'list') { return @([pscustomobject]@{ Id=[guid]'11111111-1111-1111-1111-111111111111'; Revision=1; DisplayName='before'; InstanceName='Ubuntu' }) }
        return [pscustomobject]@{ Operation=$Operation; SchemaValid=$true; Preconditions=@('validated'); ActionResults=@(); PreflightResults=@() }
      }
    }
    New-DistroNexusWorkspace -Name dry -Instance Ubuntu -ExpectedRevision 0 -WhatIf | Should -Not -BeNullOrEmpty
    Set-DistroNexusWorkspace -Id $id -Name changed -ExpectedRevision 1 -WhatIf | Should -Not -BeNullOrEmpty
    Copy-DistroNexusWorkspace -Id $id -Name copy -ExpectedRevision 1 -WhatIf | Should -Not -BeNullOrEmpty
    Remove-DistroNexusWorkspace -Id $id -ExpectedRevision 1 -WhatIf | Should -Not -BeNullOrEmpty
    Import-DistroNexusWorkspace -Path $path -ExpectedRevision 0 -WhatIf | Should -Not -BeNullOrEmpty
    Approve-DistroNexusWorkspaceTrust -Id $id -ExpectedRevision 1 -WhatIf | Should -Not -BeNullOrEmpty
    Retry-DistroNexusWorkspaceAction -Id $id -ActionId $actionId -ExpectedRevision 1 -WhatIf | Should -Not -BeNullOrEmpty
    Invoke-DistroNexusWorkspace -Id $id -ExpectedRevision 1 -WhatIf | Should -Not -BeNullOrEmpty
    $whatIfExport = Join-Path $TestDrive 'whatif-export.json'
    Export-DistroNexusWorkspace -Id $id -Path $whatIfExport -ExpectedRevision 1 -WhatIf | Should -Not -BeNullOrEmpty
    Test-Path -LiteralPath $whatIfExport | Should -BeFalse
    InModuleScope DistroNexus {
      $operations = @($script:workspaceBridgeRequests | ForEach-Object Operation)
      $operations | Should -Contain 'previewSave'
      $operations | Should -Contain 'previewDuplicate'
      $operations | Should -Contain 'previewRemove'
      $operations | Should -Contain 'previewImportDryRun'
      $operations | Should -Contain 'previewApproveTrust'
      $operations | Should -Contain 'previewRetryDryRun'
      $operations | Should -Contain 'previewLaunchDryRun'
      $operations | Should -Contain 'previewExportDryRun'
      $operations | Should -Not -Contain 'launch'
      $operations | Should -Not -Contain 'retry'
      @($script:workspaceBridgeRequests | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Token) }).Count | Should -Be 0
    }
  }

  It 'routes a trusted launch token and revision to the Core bridge without launching locally' {
    $id = [guid]::NewGuid()
    InModuleScope DistroNexus {
      $script:workspaceBridgeRequests = @()
      Mock Invoke-DistroNexusWorkspaceBridge {
        param($Operation, $Id, $ActionId, $Payload, $ExpectedRevision, $Token, $Name)
        $script:workspaceBridgeRequests += [pscustomobject]@{ Operation=$Operation; Id=$Id; ActionId=$ActionId; ExpectedRevision=$ExpectedRevision; Token=$Token }
        if ($Operation -eq 'previewLaunch') { return [pscustomobject]@{ LaunchToken='core-preview-token'; Revision=7 } }
        return [pscustomobject]@{ WorkspaceId=$Id; Actions=@([pscustomobject]@{ Outcome='Succeeded'; Code='Workspace.Action.Succeeded' }); Cancelled=$false }
      }
    }
    Invoke-DistroNexusWorkspace -Id $id -ExpectedRevision 7 -LaunchToken 'core-preview-token' -Confirm:$false | Out-Null
    InModuleScope DistroNexus {
      $script:workspaceBridgeRequests.Count | Should -Be 2
      $script:workspaceBridgeRequests[0].Operation | Should -Be 'previewLaunch'
      $script:workspaceBridgeRequests[1].Operation | Should -Be 'launch'
      $script:workspaceBridgeRequests[1].ExpectedRevision | Should -Be 7
      $script:workspaceBridgeRequests[1].Token | Should -Be 'core-preview-token'
    }
  }

  It 'routes an action-scoped capability failure retry through Core preview and token validation' {
    $id = [guid]::NewGuid(); $actionId = [guid]::NewGuid()
    InModuleScope DistroNexus {
      $script:workspaceBridgeRequests = @()
      Mock Invoke-DistroNexusWorkspaceBridge {
        param($Operation, $Id, $ActionId, $Payload, $ExpectedRevision, $Token, $Name)
        $script:workspaceBridgeRequests += [pscustomobject]@{ Operation=$Operation; Id=$Id; ActionId=$ActionId; ExpectedRevision=$ExpectedRevision; Token=$Token }
        if ($Operation -eq 'previewRetry') { return [pscustomobject]@{ LaunchToken='retry-token'; Revision=4; Commands=@('Workspace.VisualStudioCode') } }
        return [pscustomobject]@{ ActionId=$ActionId; Outcome='Failed'; Code='Workspace.Action.Failed'; Detail='Workspace.Capability.VisualStudioCode.Capability.Dependency.NotInstalled' }
      }
    }
    $preview = Get-DistroNexusWorkspaceActionRetryPreview -Id $id -ActionId $actionId
    $result = Retry-DistroNexusWorkspaceAction -Id $id -ActionId $actionId -ExpectedRevision $preview.Revision -RetryToken $preview.LaunchToken -Confirm:$false
    $result.Outcome | Should -Be 'Failed'
    $result.ActionId | Should -Be $actionId
    InModuleScope DistroNexus {
      $script:workspaceBridgeRequests[0].Operation | Should -Be 'previewRetry'
      $script:workspaceBridgeRequests[1].Operation | Should -Be 'retry'
      $script:workspaceBridgeRequests[1].ActionId | Should -Not -Be ([guid]::Empty)
      $script:workspaceBridgeRequests[1].ExpectedRevision | Should -Be 4
      $script:workspaceBridgeRequests[1].Token | Should -Be 'retry-token'
    }
  }

  It 'fails closed when an unavailable bridge path is selected' {
    Remove-Module DistroNexus -Force -ErrorAction SilentlyContinue
    $old = $env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH
    try {
      $env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = Join-Path $TestDrive 'missing-bridge.dll'
      { Import-Module "$PSScriptRoot/../../../../src/PowerShell/DistroNexus.psd1" -Force } | Should -Throw '*WorkspaceBridge*'
    } finally {
      if ($null -eq $old) { Remove-Item Env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH -ErrorAction SilentlyContinue } else { $env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = $old }
      Import-Module "$PSScriptRoot/../../../../src/PowerShell/DistroNexus.psd1" -Force
    }
  }

  It 'rejects an arbitrary bridge override instead of executing it' {
    Remove-Module DistroNexus -Force -ErrorAction SilentlyContinue
    $old = $env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH
    $fake = Join-Path $TestDrive 'bridge.ps1'; $log = Join-Path $TestDrive 'bridge.log'
    @"
`$line = [Console]::ReadLine()
`$request = `$line | ConvertFrom-Json
`$request.Operation | Set-Content -LiteralPath '$log' -NoNewline
@{ Succeeded = `$true; Value = @(); ErrorCode = `$null; ErrorMessage = `$null } | ConvertTo-Json -Compress
"@ | Set-Content -LiteralPath $fake -Encoding utf8
    try {
      $env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = $fake
      { Import-Module "$PSScriptRoot/../../../../src/PowerShell/DistroNexus.psd1" -Force } | Should -Throw '*path overrides are not supported*'
      Test-Path -LiteralPath $log | Should -BeFalse
    } finally {
      Remove-Module DistroNexus -Force -ErrorAction SilentlyContinue
      if ($null -eq $old) { Remove-Item Env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH -ErrorAction SilentlyContinue } else { $env:DISTRONEXUS_WORKSPACE_BRIDGE_PATH = $old }
      Import-Module "$PSScriptRoot/../../../../src/PowerShell/DistroNexus.psd1" -Force
    }
  }
}
