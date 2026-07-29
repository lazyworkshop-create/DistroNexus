Describe 'Workspace shortcut command uses only the closed bridge route' {
  BeforeAll { Import-Module "$PSScriptRoot/../../../../src/PowerShell/DistroNexus.psd1" -Force }

  It 'sends only WorkspaceId after confirmed consent' {
    InModuleScope DistroNexus {
      Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ OutcomeCode = 'Workspace.ShortcutCreated' } }
      $id = [guid]::NewGuid()
      New-DistroNexusWorkspaceShortcut -WorkspaceId $id -Confirm:$false | Out-Null
      Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -Exactly -ParameterFilter { $Operation -eq 'workspace.shortcut.create.v1' -and $Payload.Keys.Count -eq 1 -and $Payload.WorkspaceId -eq $id }
    }
  }

  It 'does not call the bridge for WhatIf' {
    InModuleScope DistroNexus {
      Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
      (New-DistroNexusWorkspaceShortcut -WorkspaceId ([guid]::NewGuid()) -WhatIf).OutcomeCode | Should -Be 'Workspace.ShortcutDeclined'
      Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
    }
  }
}
