Describe 'Workspace commands use only closed v1 bridge routes' {
  BeforeAll { Import-Module "$PSScriptRoot/../../../../src/PowerShell/DistroNexus.psd1" -Force }

  It 'uses token-only save, export and remove execution payloads' {
    InModuleScope DistroNexus {
      $script:requests = @()
      Mock Invoke-DistroNexusWorkspaceBridge {
        param($Operation,$Payload)
        $script:requests += [pscustomobject]@{ Operation=$Operation; Payload=$Payload }
        if ($Operation -like '*.preview.v1') { return [pscustomobject]@{ PreviewToken=('a' * 64) } }
        if ($Operation -eq 'workspace.export.execute.v1') { return [pscustomobject]@{ Content='{}' } }
      }
      $id = [guid]::NewGuid()
      Get-DistroNexusWorkspaceSavePreview -Definition @{ Id=$id } -ExpectedRevision 0 | Out-Null
      Save-DistroNexusWorkspace -PreviewToken ('a' * 64) -Confirm:$false | Out-Null
      Get-DistroNexusWorkspaceExportPreview -Id $id -ExpectedRevision 1 | Out-Null
      Export-DistroNexusWorkspace -PreviewToken ('a' * 64) -Confirm:$false | Out-Null
      Get-DistroNexusWorkspaceRemovePreview -Id $id -ExpectedRevision 1 | Out-Null
      Remove-DistroNexusWorkspace -PreviewToken ('a' * 64) -Confirm:$false | Out-Null
      $script:requests.Operation | Should -Be @('workspace.save.preview.v1','workspace.save.execute.v1','workspace.export.preview.v1','workspace.export.execute.v1','workspace.remove.preview.v1','workspace.remove.execute.v1')
      @($script:requests | Where-Object { $_.Operation -like '*.execute.v1' -and $_.Payload.Keys.Count -ne 1 }).Count | Should -Be 0
    }
  }

  It 'does not call the bridge for token-consuming WhatIf operations' {
    InModuleScope DistroNexus {
      Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
      Save-DistroNexusWorkspace -PreviewToken ('a' * 64) -WhatIf
      Invoke-DistroNexusWorkspace -PreviewToken ('a' * 64) -WhatIf
      Stop-DistroNexusWorkspaceOperation -OperationId ('b' * 64) -WhatIf
      Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
    }
  }

  It 'uses v1 operation status and cancellation routes' {
    InModuleScope DistroNexus {
      Mock Invoke-DistroNexusWorkspaceBridge { [pscustomobject]@{ IsTerminal=$false } }
      Get-DistroNexusWorkspaceOperation -OperationId ('a' * 64) | Out-Null
      Stop-DistroNexusWorkspaceOperation -OperationId ('a' * 64) -Confirm:$false | Out-Null
      Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'workspace.operation.status.v1' -and $Payload.OperationId -eq ('a' * 64) }
      Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 1 -ParameterFilter { $Operation -eq 'workspace.cancel.v1' -and $Payload.OperationId -eq ('a' * 64) }
    }
  }
}
