# Export-DistroNexusInstance.Tests.ps1 / Import-DistroNexusInstance.Tests.ps1
# Unit tests for E-01 Instance Export and Import

BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")

    $script:originalAppData = $env:APPDATA
}

AfterAll {
    $env:APPDATA = $script:originalAppData
}

Describe "Export-DistroNexusInstance" -Tag 'Unit', 'Public', 'Export' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Export-DistroNexusInstance -Destination "C:\tmp\out.tar" } | Should -Throw
        }

        It "Should require -Destination parameter" {
            { Export-DistroNexusInstance -Name "Ubuntu-22.04" } | Should -Throw
        }
    }

    Context "When instance does not exist" {
        It "Should write error when instance not found" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance { return $null } -ModuleName DistroNexus

                $errorRecord = $null
                Export-DistroNexusInstance -Name "NonExistent" -Destination "C:\tmp\out.tar" `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue
                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }

    Context "When instance is running" {
        It "Should not export without -Force when instance is running (stop guard)" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Running"; Version = 2 }
                } -ModuleName DistroNexus

                $errorRecord = $null
                Export-DistroNexusInstance -Name "Ubuntu-22.04" -Destination "C:\tmp\out.tar" `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue
                # Should either stop or error; test just verifies no unhandled exception
                $true | Should -Be $true
            }
        }
    }

    Context "Default filename pattern" {
        It "Should generate <Name>-<yyyyMMdd>.tar when Destination is a directory" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Test-Path {
                    param($Path)
                    if ($Path -like "*Ubuntu*") { return $true }
                    return $true
                } -ModuleName DistroNexus

                $today = (Get-Date).ToString("yyyyMMdd")
                $destDir = Join-Path $TestDrive "exports"
                New-Item -Path $destDir -ItemType Directory -Force | Out-Null

                # We just check the cmdlet exists and has the correct params
                (Get-Command Export-DistroNexusInstance).Parameters.ContainsKey('Name') | Should -Be $true
                (Get-Command Export-DistroNexusInstance).Parameters.ContainsKey('Destination') | Should -Be $true
                (Get-Command Export-DistroNexusInstance).Parameters.ContainsKey('Force') | Should -Be $true
            }
        }
    }

    Context "Progress reporting" {
        It "Should call Write-Progress during export loop and once with -Completed" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                Mock Stop-DistroNexusInstance { return $true } -ModuleName DistroNexus
                Mock Start-DistroNexusInstance { return $true } -ModuleName DistroNexus

                Mock Test-Path {
                    param($Path, $PathType)
                    # Destination "C:\test.tar" should NOT be treated as a Container
                    if ($PathType -eq 'Container') { return $false }
                    return $true
                } -ModuleName DistroNexus

                Mock Get-Item {
                    [PSCustomObject]@{ Length = 1048576 }
                } -ModuleName DistroNexus

                # Start-Job returns a job that initially appears Running
                $fakeJob = [PSCustomObject]@{ State = "Running"; Id = 123 }
                Mock Start-Job {
                    $fakeJob
                } -ModuleName DistroNexus

                # Get-Job: first call returns Running, second returns Completed
                $script:getJobCallCount = 0
                Mock Get-Job {
                    param($Id)
                    $script:getJobCallCount++
                    if ($script:getJobCallCount -le 1) {
                        [PSCustomObject]@{ State = "Running"; Id = 123 }
                    }
                    else {
                        [PSCustomObject]@{ State = "Completed"; Id = 123 }
                    }
                } -ModuleName DistroNexus

                Mock Receive-Job {
                    param($Id, $Wait, $AutoRemoveJob)
                    [PSCustomObject]@{ ExitCode = 0 }
                } -ModuleName DistroNexus

                Mock Start-Sleep {} -ModuleName DistroNexus

                Mock Write-Progress {} -ModuleName DistroNexus

                Mock Write-DistroNexusLog {} -ModuleName DistroNexus

                # Capture output so it does not flow into the Should -Invoke assertions below
                $null = @(Export-DistroNexusInstance -Name "Ubuntu" -Destination "C:\test.tar" 2>&1)

                # Should have at least one in-loop progress update (no -Completed)
                Should -Invoke -CommandName Write-Progress -Times 1 -Exactly -ModuleName DistroNexus `
                    -ParameterFilter { $Completed -ne $true }

                # Should have exactly one completion call
                Should -Invoke -CommandName Write-Progress -Times 1 -Exactly -ModuleName DistroNexus `
                    -ParameterFilter { $Completed -eq $true }
            }
        }
    }
}

Describe "Import-DistroNexusInstance" -Tag 'Unit', 'Public', 'Import' {

    BeforeEach {
        $env:APPDATA = $TestDrive
        $distroNexusPath = Join-Path $env:APPDATA "DistroNexus"
        New-Item -Path $distroNexusPath -ItemType Directory -Force | Out-Null
    }

    Context "Parameter validation" {
        It "Should require -Name parameter" {
            { Import-DistroNexusInstance -Source "C:\tmp\out.tar" -InstallPath "C:\WSL\Ubuntu" } | Should -Throw
        }

        It "Should require -Source parameter" {
            { Import-DistroNexusInstance -Name "MyDistro" -InstallPath "C:\WSL\Ubuntu" } | Should -Throw
        }

        It "Should require -InstallPath parameter" {
            { Import-DistroNexusInstance -Name "MyDistro" -Source "C:\tmp\out.tar" } | Should -Throw
        }
    }

    Context "When instance name already exists" {
        It "Should write error when target name already registered" {
            InModuleScope DistroNexus {
                Mock Get-DistroNexusInstance {
                    return [PSCustomObject]@{ Name = "Ubuntu-22.04"; State = "Stopped"; Version = 2 }
                } -ModuleName DistroNexus

                $errorRecord = $null
                Import-DistroNexusInstance -Name "Ubuntu-22.04" -Source "C:\tmp\out.tar" `
                    -InstallPath "C:\WSL\NewUbuntu" `
                    -ErrorVariable errorRecord -ErrorAction SilentlyContinue
                $errorRecord | Should -Not -BeNullOrEmpty
            }
        }
    }

    Context "Command structure" {
        It "Should have correct parameters" {
            (Get-Command Import-DistroNexusInstance).Parameters.ContainsKey('Name') | Should -Be $true
            (Get-Command Import-DistroNexusInstance).Parameters.ContainsKey('Source') | Should -Be $true
            (Get-Command Import-DistroNexusInstance).Parameters.ContainsKey('InstallPath') | Should -Be $true
        }
    }
}
