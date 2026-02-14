# TerminalLauncher.Tests.ps1
# Unit tests for TerminalLauncher.ps1 Private functions

BeforeAll {
    # Import the module
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    
    # Import test helpers
    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")
}

Describe "Find-TerminalPath" -Tag 'Unit', 'TerminalLauncher' {
    Context "When detecting available terminals" {
        It "Should return a PSCustomObject with required properties" {
            InModuleScope DistroNexus {
                # Act
                $result = Find-TerminalPath
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.PSObject.Properties.Name | Should -Contain 'Path'
                $result.PSObject.Properties.Name | Should -Contain 'Type'
                $result.PSObject.Properties.Name | Should -Contain 'DisplayName'
            }
        }
        
        It "Should return CMD when PreferredTerminal is CMD" {
            InModuleScope DistroNexus {
                # Act
                $result = Find-TerminalPath -PreferredTerminal 'CMD'
                
                # Assert
                $result.Type | Should -Be 'CMD'
                $result.DisplayName | Should -Be 'Command Prompt'
                $result.Path | Should -Match 'cmd\.exe'
            }
        }
        
        It "Should return Windows Terminal if available and Auto is specified" {
            InModuleScope DistroNexus {
                # Act
                $result = Find-TerminalPath -PreferredTerminal 'Auto'
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                # Type should be either WindowsTerminal or CMD
                $result.Type | Should -BeIn @('WindowsTerminal', 'CMD')
            }
        }
        
        It "Should fallback to CMD if Windows Terminal not found" {
            InModuleScope DistroNexus {
                # Arrange - Mock Get-Command to simulate Windows Terminal not found
                Mock Get-Command {
                    if ($Name -eq 'wt.exe') {
                        throw "Command not found"
                    }
                } -ModuleName DistroNexus
                
                # Act
                $result = Find-TerminalPath -PreferredTerminal 'WindowsTerminal'
                
                # Assert
                $result.Type | Should -Be 'CMD'
            }
        }
        
        It "Should return valid Path for CMD" {
            InModuleScope DistroNexus {
                # Act
                $result = Find-TerminalPath -PreferredTerminal 'CMD'
                
                # Assert
                Test-Path $result.Path | Should -BeTrue
            }
        }
    }
    
    Context "When handling terminal preferences" {
        It "Should accept valid PreferredTerminal values" {
            InModuleScope DistroNexus {
                # Act & Assert
                { Find-TerminalPath -PreferredTerminal 'Auto' } | Should -Not -Throw
                { Find-TerminalPath -PreferredTerminal 'WindowsTerminal' } | Should -Not -Throw
                { Find-TerminalPath -PreferredTerminal 'CMD' } | Should -Not -Throw
            }
        }
        
        It "Should use Auto as default when not specified" {
            InModuleScope DistroNexus {
                # Act
                $result = Find-TerminalPath
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
            }
        }
    }
}

Describe "Invoke-Terminal" -Tag 'Unit', 'TerminalLauncher' {
    Context "When launching terminal" {
        It "Should reject empty InstanceName parameter" {
            InModuleScope DistroNexus {
                # Act & Assert
                { Invoke-Terminal -InstanceName '' -ErrorAction Stop } | Should -Throw
            }
        }
        
        It "Should accept valid parameters without throwing" {
            InModuleScope DistroNexus {
                # Arrange - Mock Start-Process to avoid actually launching
                Mock Start-Process { 
                    return [PSCustomObject]@{ Id = 12345 }
                } -ModuleName DistroNexus
                
                # Act & Assert
                { Invoke-Terminal -InstanceName "Ubuntu-22.04" -WhatIf:$false } | Should -Not -Throw
            }
        }
        
        It "Should pass instance name to terminal command" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Start-Process {
                    param($FilePath, $ArgumentList)
                    $ArgumentList -join ' ' | Should -Match 'Ubuntu-22.04'
                    return [PSCustomObject]@{ Id = 12345 }
                } -ModuleName DistroNexus
                
                # Act
                Invoke-Terminal -InstanceName "Ubuntu-22.04" -WhatIf:$false
                
                # Assert - verified in Mock
            }
        }
        
        It "Should include StartPath in command when specified" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Start-Process {
                    param($FilePath, $ArgumentList)
                    $ArgumentList -join ' ' | Should -Match '/var/www'
                    return [PSCustomObject]@{ Id = 12345 }
                } -ModuleName DistroNexus
                
                # Act
                Invoke-Terminal -InstanceName "Ubuntu" -StartPath "/var/www" -WhatIf:$false
                
                # Assert - verified in Mock
            }
        }
        
        It "Should use CMD when PreferredTerminal is CMD" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Start-Process {
                    param($FilePath)
                    $FilePath | Should -Match 'cmd\.exe'
                    return [PSCustomObject]@{ Id = 12345 }
                } -ModuleName DistroNexus
                
                # Act
                Invoke-Terminal -InstanceName "Ubuntu" -PreferredTerminal "CMD" -WhatIf:$false
                
                # Assert - verified in Mock
            }
        }
        
        It "Should return true on successful launch" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Start-Process {
                    return [PSCustomObject]@{ Id = 12345 }
                } -ModuleName DistroNexus
                
                # Act
                $result = Invoke-Terminal -InstanceName "Ubuntu" -WhatIf:$false
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should respect WhatIf parameter" {
            InModuleScope DistroNexus {
                # Arrange
                $startProcessCalled = $false
                Mock Start-Process {
                    $script:startProcessCalled = $true
                    return [PSCustomObject]@{ Id = 12345 }
                } -ModuleName DistroNexus
                
                # Act
                $result = Invoke-Terminal -InstanceName "Ubuntu" -WhatIf
                
                # Assert
                $result | Should -BeFalse
                $startProcessCalled | Should -BeFalse
            }
        }
        
        It "Should use NoWait by default" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Start-Process {
                    param($FilePath, $ArgumentList, $PassThru, $Wait)
                    $PassThru | Should -BeTrue
                    $Wait | Should -Not -BeTrue
                    return [PSCustomObject]@{ Id = 12345 }
                } -ModuleName DistroNexus
                
                # Act
                Invoke-Terminal -InstanceName "Ubuntu" -WhatIf:$false
                
                # Assert - verified in Mock
            }
        }
    }
    
    Context "When handling errors" {
        It "Should throw on Start-Process failure" {
            InModuleScope DistroNexus {
                # Arrange
                Mock Start-Process {
                    throw "Failed to start process"
                } -ModuleName DistroNexus
                
                # Act & Assert
                { Invoke-Terminal -InstanceName "Ubuntu" -WhatIf:$false } | Should -Throw
            }
        }
    }
}

Describe "Test-TerminalAvailable" -Tag 'Unit', 'TerminalLauncher' {
    Context "When checking terminal availability" {
        It "Should return boolean value" {
            InModuleScope DistroNexus {
                # Act
                $result = Test-TerminalAvailable -TerminalType 'CMD'
                
                # Assert
                $result | Should -BeOfType [bool]
            }
        }
        
        It "Should return true for CMD (always available on Windows)" {
            InModuleScope DistroNexus {
                # Act
                $result = Test-TerminalAvailable -TerminalType 'CMD'
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should return boolean for Windows Terminal check" {
            InModuleScope DistroNexus {
                # Act
                $result = Test-TerminalAvailable -TerminalType 'WindowsTerminal'
                
                # Assert
                $result | Should -BeOfType [bool]
            }
        }
        
        It "Should accept only valid TerminalType values" {
            InModuleScope DistroNexus {
                # Act & Assert
                { Test-TerminalAvailable -TerminalType 'CMD' } | Should -Not -Throw
                { Test-TerminalAvailable -TerminalType 'WindowsTerminal' } | Should -Not -Throw
            }
        }
    }
}

Describe "Get-AvailableTerminals" -Tag 'Unit', 'TerminalLauncher' {
    Context "When getting available terminals" {
        It "Should return an array of terminals" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-AvailableTerminals
                
                # Assert
                $result | Should -Not -BeNullOrEmpty
                $result.Count | Should -BeGreaterThan 0
            }
        }
        
        It "Should include CMD in the list" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-AvailableTerminals
                
                # Assert
                $cmdTerminal = $result | Where-Object { $_.Type -eq 'CMD' }
                $cmdTerminal | Should -Not -BeNullOrEmpty
                $cmdTerminal.Available | Should -BeTrue
            }
        }
        
        It "Should include Windows Terminal in the list" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-AvailableTerminals
                
                # Assert
                $wtTerminal = $result | Where-Object { $_.Type -eq 'WindowsTerminal' }
                $wtTerminal | Should -Not -BeNullOrEmpty
                $wtTerminal.PSObject.Properties.Name | Should -Contain 'Available'
            }
        }
        
        It "Should return terminals with all required properties" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-AvailableTerminals
                
                # Assert
                foreach ($terminal in $result) {
                    $terminal.PSObject.Properties.Name | Should -Contain 'Type'
                    $terminal.PSObject.Properties.Name | Should -Contain 'Path'
                    $terminal.PSObject.Properties.Name | Should -Contain 'DisplayName'
                    $terminal.PSObject.Properties.Name | Should -Contain 'Available'
                }
            }
        }
        
        It "Should have at least one available terminal" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-AvailableTerminals
                
                # Assert
                $availableCount = ($result | Where-Object { $_.Available }).Count
                $availableCount | Should -BeGreaterThan 0
            }
        }
    }
}
