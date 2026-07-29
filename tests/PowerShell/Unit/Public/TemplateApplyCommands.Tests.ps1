Describe 'Template apply commands use only closed v1 routes' {
  BeforeAll { Import-Module "$PSScriptRoot/../../../../src/PowerShell/DistroNexus.psd1" -Force }

  It 'uses the preview token as the only execute authority' {
    InModuleScope DistroNexus {
      $script:requests = @()
      Mock Invoke-DistroNexusWorkspaceBridge { param($Operation,$Payload) $script:requests += [pscustomobject]@{ Operation=$Operation; Payload=$Payload }; [pscustomobject]@{ OperationId=('a' * 64) } }
      New-DistroNexusTemplateApplyPreview -InstanceName Ubuntu -TemplateId dev -Variables @{ version='1' } -DeclineRecoveryOffer -Confirm:$false | Out-Null
      Start-DistroNexusTemplateApply -PreviewToken ('a' * 64) -Confirm:$false | Out-Null
      Get-DistroNexusTemplateApplyOperation -OperationId ('a' * 64) | Out-Null
      Stop-DistroNexusTemplateApply -OperationId ('a' * 64) -Confirm:$false | Out-Null
      $script:requests.Operation | Should -Be @('template.apply.preview.v1','template.apply.execute.v1','template.apply.status.v1','template.apply.cancel.v1')
      $script:requests[1].Payload.Keys.Count | Should -Be 1
      $script:requests[1].Payload.PreviewToken | Should -Be ('a' * 64)
    }
  }

  It 'does not invoke mutating routes under WhatIf' {
    InModuleScope DistroNexus {
      Mock Invoke-DistroNexusWorkspaceBridge { throw 'must not execute' }
      New-DistroNexusTemplateApplyPreview -InstanceName Ubuntu -TemplateId dev -WhatIf
      Start-DistroNexusTemplateApply -PreviewToken ('a' * 64) -WhatIf
      Stop-DistroNexusTemplateApply -OperationId ('a' * 64) -WhatIf
      Should -Invoke Invoke-DistroNexusWorkspaceBridge -Times 0 -Exactly
    }
  }
}
