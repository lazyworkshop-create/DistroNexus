# Save-DistroNexusPackage.Tests.ps1
# Unit tests for Save-DistroNexusPackage Public Cmdlet (Batch Download功能)

BeforeAll {
    $modulePath = Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
    $modulePath = Join-Path $modulePath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    
    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")
}

Describe "Save-DistroNexusPackage" -Tag 'Unit', 'Public', 'Download' {
    BeforeEach {
        $script:testOutputPath = Join-Path $TestDrive "packages"
        New-Item -Path $script:testOutputPath -ItemType Directory -Force | Out-Null
    }
    
    Context "When downloading with -Family parameter" {
        It "Should accept valid family names" {
            # Act & Assert
            { Save-DistroNexusPackage -Family "Ubuntu" -OutputPath $script:testOutputPath -WhatIf } | Should -Not -Throw
        }
    }
    
    Context "When downloading with -All parameter" {
        It "Should process all distros" -Skip {
            # Integration test - requires actual catalog and network
        }
    }
    
    Context "When handling concurrent downloads" {
        It "Should manage parallel download jobs" -Skip {
            # Integration test - requires network
        }
    }
}
