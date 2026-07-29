# Unit tests for PowerShell consent semantics on instance-tag mutations.

BeforeAll {
    $script:rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $script:rootPath "src\PowerShell\DistroNexus.psd1") -Force
    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
    Remove-Item Function:\Invoke-DeclinedTagMutation -ErrorAction SilentlyContinue
}

function global:Invoke-DeclinedTagMutation {
    param([Parameter(Mandatory)][string]$Command)

    $env:DISTRONEXUS_TEST_APPDATA = $env:APPDATA
    $modulePath = Join-Path $script:rootPath "src\PowerShell\DistroNexus.psd1"
    'N' | & pwsh -NoProfile -Command "& { Import-Module '$modulePath' -Force -DisableNameChecking; `$env:APPDATA = `$env:DISTRONEXUS_TEST_APPDATA; $Command -Confirm }" | Out-Null

    $LASTEXITCODE | Should -Be 0
}

Describe "Instance tag mutation consent" -Tag 'Unit', 'Public', 'Tags' {
    BeforeEach {
        $env:APPDATA = $TestDrive
    }

    It "does not persist an added tag with WhatIf" {
        Add-DistroNexusInstanceTag -Name "Ubuntu" -Tag "dev" -WhatIf

        Test-Path (Join-Path $env:APPDATA "DistroNexus\settings.json") | Should -BeFalse
    }

    It "does not replace tags with WhatIf" {
        Set-DistroNexusInstanceTag -Name "Ubuntu" -Tags @("dev")

        Set-DistroNexusInstanceTag -Name "Ubuntu" -Tags @("prod") -WhatIf

        (Get-DistroNexusInstanceTag -Name "Ubuntu").Tags | Should -Be @("dev")
    }

    It "does not remove a tag with WhatIf" {
        Set-DistroNexusInstanceTag -Name "Ubuntu" -Tags @("dev")

        Remove-DistroNexusInstanceTag -Name "Ubuntu" -Tag "dev" -WhatIf

        (Get-DistroNexusInstanceTag -Name "Ubuntu").Tags | Should -Be @("dev")
    }

    It "does not migrate tags with WhatIf" {
        Set-DistroNexusInstanceTag -Name "Ubuntu" -Tags @("dev")

        Rename-DistroNexusInstanceTags -OldName "Ubuntu" -NewName "Ubuntu-Dev" -WhatIf

        (Get-DistroNexusInstanceTag -Name "Ubuntu").Tags | Should -Be @("dev")
        (Get-DistroNexusInstanceTag -Name "Ubuntu-Dev").Tags | Should -BeNullOrEmpty
    }

    It "does not persist an added tag when confirmation is declined" {
        Remove-Item -LiteralPath (Join-Path $env:APPDATA "DistroNexus") -Recurse -Force -ErrorAction SilentlyContinue
        Invoke-DeclinedTagMutation 'Add-DistroNexusInstanceTag -Name Ubuntu -Tag dev'

        Test-Path (Join-Path $env:APPDATA "DistroNexus\settings.json") | Should -BeFalse
    }

    It "does not replace tags when confirmation is declined" {
        Set-DistroNexusInstanceTag -Name "Ubuntu" -Tags @("dev")

        Invoke-DeclinedTagMutation 'Set-DistroNexusInstanceTag -Name Ubuntu -Tags @(''prod'')'

        (Get-DistroNexusInstanceTag -Name "Ubuntu").Tags | Should -Be @("dev")
    }

    It "does not remove tags when confirmation is declined" {
        Set-DistroNexusInstanceTag -Name "Ubuntu" -Tags @("dev")

        Invoke-DeclinedTagMutation 'Remove-DistroNexusInstanceTag -Name Ubuntu -Tag dev'

        (Get-DistroNexusInstanceTag -Name "Ubuntu").Tags | Should -Be @("dev")
    }

    It "does not migrate tags when confirmation is declined" {
        Set-DistroNexusInstanceTag -Name "Ubuntu" -Tags @("dev")

        Invoke-DeclinedTagMutation 'Rename-DistroNexusInstanceTags -OldName Ubuntu -NewName Ubuntu-Dev'

        (Get-DistroNexusInstanceTag -Name "Ubuntu").Tags | Should -Be @("dev")
        (Get-DistroNexusInstanceTag -Name "Ubuntu-Dev").Tags | Should -BeNullOrEmpty
    }

    It "migrates tags on confirmed rename" {
        Set-DistroNexusInstanceTag -Name "Ubuntu" -Tags @("dev", "docker")

        Rename-DistroNexusInstanceTags -OldName "Ubuntu" -NewName "Ubuntu-Dev" -Confirm:$false

        (Get-DistroNexusInstanceTag -Name "Ubuntu-Dev").Tags | Should -Be @("dev", "docker")
        (Get-DistroNexusInstanceTag -Name "Ubuntu").Tags | Should -BeNullOrEmpty
    }
}
