# Rename-DistroNexusInstance.Tests.ps1
# Unit tests for E06-2: tag migration on instance rename

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $script:originalAppData = $env:APPDATA
    $script:originalTemp    = $env:TEMP
}

AfterAll {
    $env:APPDATA = $script:originalAppData
    $env:TEMP    = $script:originalTemp
}

Describe "Rename-DistroNexusInstance tag migration" -Tag 'Unit', 'Public', 'Rename', 'Tags' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $env:TEMP    = $TestDrive

        # Create a dummy tar so the finally-block Remove-Item does not error
        $dummyTar = Join-Path $TestDrive "TestOld-rename.tar"
        Set-Content $dummyTar -Value "" -Encoding UTF8
    }

    It "calls Rename-DistroNexusInstanceTags after a successful rename" {
        InModuleScope DistroNexus {
            # Arrange
            Mock Initialize-DistroNexusLogger {}
            Mock Write-DistroNexusLog {}

            $fakeInstance = [PSCustomObject]@{ Name = 'TestOld'; BasePath = 'C:\WSL\TestOld' }
            Mock Get-DistroNexusInstance {
                param($Name, [switch]$ForceUpdate)
                if ($ForceUpdate) { return @() }
                return @($fakeInstance)
            } -ModuleName DistroNexus

            # Simulate wsl --export, --unregister, --import all succeeding
            Mock wsl {
                $global:LASTEXITCODE = 0
            } -ModuleName DistroNexus

            Mock Rename-DistroNexusInstanceTags {} -ModuleName DistroNexus

            # Act
            $result = Rename-DistroNexusInstance -Name "TestOld" -NewName "TestNew" -Confirm:$false

            # Assert
            Should -Invoke Rename-DistroNexusInstanceTags -Times 1 -Exactly -ModuleName DistroNexus
        }
    }

    It "does not call Rename-DistroNexusInstanceTags when instance is not found" {
        InModuleScope DistroNexus {
            # Arrange
            Mock Initialize-DistroNexusLogger {}
            Mock Write-DistroNexusLog {}

            Mock Get-DistroNexusInstance { return @() } -ModuleName DistroNexus
            Mock Rename-DistroNexusInstanceTags {} -ModuleName DistroNexus

            # Act
            $result = Rename-DistroNexusInstance -Name "NoSuchInstance" -NewName "TestNew" -Confirm:$false

            # Assert
            Should -Invoke Rename-DistroNexusInstanceTags -Times 0 -Exactly -ModuleName DistroNexus
        }
    }
}
