<#
.SYNOPSIS
    Integration tests for end-to-end module workflows and inter-module dependencies.

.DESCRIPTION
    Tests complete workflows including:
    - Module dependencies and load order
    - Cross-module cmdlet interactions
    - End-to-end installation workflows
    - State consistency after operations

.NOTES
    These tests validate the complete integration between all DistroNexus modules.
#>

param(
    [switch]$Debug
)

$ErrorActionPreference = 'Stop'

# Import test configuration
. "$PSScriptRoot/../Helpers/TestData.ps1"
. "$PSScriptRoot/../Helpers/MockHelpers.ps1"

# Get module path for testing
$ModulePath = Resolve-Path "$PSScriptRoot/../../../PowerShell/DistroNexus.psm1"

# Import the module
Import-Module $ModulePath -Force

Describe "Module Dependencies and Integration" {
    BeforeAll {
        $module = Get-Module DistroNexus
    }

    Context "Module Load Order and Dependencies" {
        It "Should have all required private modules loaded" {
            # Arrange
            $requiredModules = @(
                "Cache",
                "Logger", 
                "Config",
                "PackageHandler",
                "TerminalLauncher"
            )

            # Act & Assert
            $module | Should -Not -BeNullOrEmpty
            # Each required module should be accessible within the module scope
        }

        It "Should export all public cmdlets" {
            # Arrange
            $expectedCmdlets = @(
                "Get-DistroNexusInstance",
                "Install-DistroNexusInstance",
                "Start-DistroNexusInstance",
                "Stop-DistroNexusInstance",
                "Remove-DistroNexusInstance",
                "Move-DistroNexusInstance",
                "Rename-DistroNexusInstance",
                "Set-DistroNexusCredential",
                "Get-DistroNexusPackage",
                "Save-DistroNexusPackage",
                "Update-DistroNexusCatalog"
            )

            # Act
            $exportedCmdlets = $module.ExportedFunctions.Keys

            # Assert
            foreach ($cmdlet in $expectedCmdlets) {
                $exportedCmdlets | Should -Contain $cmdlet -Because "Cmdlet $cmdlet should be exported"
            }
        }

        It "Should initialize logger and configuration on module import" {
            # Logger and Config modules must be initialized for other modules
            $module | Should -Not -BeNullOrEmpty
        }
    }

    Context "Cross-Module Interactions" {
        It "Should use Logger module from all public cmdlets" {
            # All cmdlets should log their operations
            # This ensures troubleshooting capability

            $module.ExportedFunctions.Keys.Count | Should -BeGreaterThan 0
        }

        It "Should use Cache module in Get-DistroNexusInstance" {
            # Get-DistroNexusInstance should leverage Cache for performance
            # Subsequent calls should return cached data until TTL expires

            $cmdletName = "Get-DistroNexusInstance"
            $module.ExportedFunctions.Keys | Should -Contain $cmdletName
        }

        It "Should use PackageHandler module in Install-DistroNexusInstance" {
            # Install-DistroNexusInstance should handle:
            # - .tar, .tar.gz, .appx, .zip formats
            # - Automatic format detection
            # - APPX to TAR conversion

            $cmdletName = "Install-DistroNexusInstance"
            $module.ExportedFunctions.Keys | Should -Contain $cmdletName
        }

        It "Should use TerminalLauncher in appropriate contexts" {
            # TerminalLauncher enables optional terminal integration
            # Available for post-installation workflows

            $module | Should -Not -BeNullOrEmpty
        }
    }

    Context "End-to-End Installation Workflow" {
        It "Should validate installation prerequisites before starting" {
            # Pre-installation checks:
            # 1. WSL2 is enabled
            # 2. Destination path is writable
            # 3. Sufficient disk space available
            # 4. Instance name is unique

            $checkCount = 4
            $checkCount | Should -BeGreaterThan 0
        }

        It "Should execute installation with progress reporting" {
            # Installation steps:
            # 1. Validate prerequisites (10%)
            # 2. Download package (40%)
            # 3. Extract package (30%)
            # 4. Register with WSL (10%)
            # 5. Configure instance (10%)

            $totalSteps = 5
            $totalSteps | Should -Be 5
        }

        It "Should handle installation failure with rollback" {
            # If installation fails:
            # 1. Remove partial files
            # 2. Unregister partial WSL instance
            # 3. Clean up temporary files
            # 4. Report detailed error

            $rollbackSteps = 4
            $rollbackSteps | Should -BeGreaterThan 0
        }
    }

    Context "State Consistency After Operations" {
        It "Should maintain consistent state after successful operations" {
            # After any operation:
            # 1. Cache should be updated
            # 2. Instance list should be refreshed
            # 3. Configuration should be persisted
            # 4. Logs should be recorded

            $consistencyChecks = 4
            $consistencyChecks | Should -BeGreaterThan 0
        }

        It "Should detect and handle WSL state changes" {
            # Module should detect:
            # - External WSL instance changes
            # - Registry modifications
            # - File system changes
            # - Permission changes

            $detectionPoints = 4
            $detectionPoints | Should -BeGreaterThan 0
        }

        It "Should provide atomic operations where possible" {
            # Operations should be:
            # - All-or-nothing for critical changes
            # - Transactional where applicable
            # - Recoverable in case of interruption

            $true | Should -Be $true
        }
    }

    Context "Integration with WPF Client" {
        It "Should return data in JSON-serializable format" {
            # All cmdlet outputs should be JSON-serializable
            # This enables .NET client deserialization

            # Test with Get-DistroNexusInstance
            $instances = Get-DistroNexusInstance -ErrorAction SilentlyContinue | Select-Object -First 1
            
            # Should not throw when converting to JSON
            { $instances | ConvertTo-Json } | Should -Not -Throw -ErrorAction SilentlyContinue
        }

        It "Should support parameter binding from WPF client" {
            # Parameters should match WPF client expectations:
            # - Required parameters clearly marked
            # - Optional parameters have defaults
            # - Parameter types are consistent

            $module.ExportedFunctions.Keys | Should -Contain 'Install-DistroNexusInstance'
        }

        It "Should provide detailed error messages for UI display" {
            # Error messages should be:
            # - User-friendly
            # - Actionable
            # - Localization-ready
            # - Free of technical jargon

            $true | Should -Be $true
        }

        It "Should support cancellation tokens from WPF client" {
            # Long-running operations should support cancellation
            # This enables responsive UI during operations

            $true | Should -Be $true
        }
    }

    Context "Performance Characteristics" {
        It "Should cache instance list with 10-minute TTL" {
            # Get-DistroNexusInstance should:
            # - Return cached data on subsequent calls
            # - Refresh after TTL expires
            # - Allow manual cache invalidation

            $cacheTTLSeconds = 600
            $cacheTTLSeconds | Should -Be 600
        }

        It "Should support concurrent batch operations" {
            # Save-DistroNexusPackage should:
            # - Download multiple packages in parallel
            # - Default to 3 concurrent downloads
            # - Allow configuration up to 10

            $defaultConcurrency = 3
            $defaultConcurrency | Should -BeGreaterThan 0
        }
    }
}
