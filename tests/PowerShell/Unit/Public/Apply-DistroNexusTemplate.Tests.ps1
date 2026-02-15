# Apply-DistroNexusTemplate.Tests.ps1

Describe "Apply-DistroNexusTemplate" -Tag 'Unit', 'Public' {

    BeforeAll {
    # Import the module
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force

}
    
    Context "Applying Templates" {
        It "Should execute Bash script" -Skip:(-not (($env:DISTRONEXUS_RUN_WSL2_TESTS -eq '1') -and ($null -ne (Get-Command wsl.exe -ErrorAction SilentlyContinue)))) {
            InModuleScope DistroNexus {
                $template = [PSCustomObject]@{
                    Id = "t1"
                    Name = "Test Template"
                    Scripts = @(
                        [PSCustomObject]@{
                            Name = "S1"
                            Type = "Bash" # or 0
                            Content = "echo hello"
                            Order = 1
                        }
                    )
                }

                Mock wsl.exe {
                    $global:LASTEXITCODE = 0
                    if ($Args -contains '--list') {
                        return "Ubuntu-22.04"
                    }
                } -ModuleName DistroNexus

                { Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -Template $template -ErrorAction Stop } | Should -Not -Throw
            }
        }

        It "Should execute PowerShell script" {
             InModuleScope DistroNexus {
                 $template = [PSCustomObject]@{
                    Id = "t2"
                    Name = "PS Template"
                    Scripts = @(
                        [PSCustomObject]@{
                            Name = "S2"
                            Type = "PowerShell"
                            Content = "Write-Output 'PS'"
                            Order = 1
                        }
                    )
                }

                Mock wsl.exe { return "Ubuntu-22.04" } -ModuleName DistroNexus -ParameterFilter { $Args -contains "--list" }
                Mock Invoke-Expression { }

                Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -Template $template

                Assert-MockCalled Invoke-Expression -Times 1
             }
        }
        
        It "Should skip script if Content is empty" {
             InModuleScope DistroNexus {
                 $template = [PSCustomObject]@{
                    Id = "t3"
                    Name = "Empty Template"
                    Scripts = @(
                        [PSCustomObject]@{
                            Name = "S3"
                            Type = "Bash"
                            Content = ""
                            Order = 1
                        }
                    )
                }
                
                Mock wsl.exe { return "Ubuntu-22.04" } -ModuleName DistroNexus -ParameterFilter { $Args -contains "--list" }

                Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -Template $template

                # Should be called 1 time only (for list check), not for execution
                Assert-MockCalled wsl.exe -Times 1 
             }
        }

        It "Should execute script content resolved from ScriptPath" -Skip:(-not (($env:DISTRONEXUS_RUN_WSL2_TESTS -eq '1') -and ($null -ne (Get-Command wsl.exe -ErrorAction SilentlyContinue)))) {
            InModuleScope DistroNexus {
                $template = [PSCustomObject]@{
                    Id = "t4"
                    Name = "ScriptPath Template"
                    Scripts = @(
                        [PSCustomObject]@{
                            Name = "S4"
                            Type = "Bash"
                            ScriptPath = "templates/test/install.sh"
                            Order = 1
                        }
                    )
                }

                Mock wsl.exe {
                    $global:LASTEXITCODE = 0
                    if ($Args -contains '--list') {
                        return "Ubuntu-22.04"
                    }
                } -ModuleName DistroNexus
                Mock Test-Path { return $true } -ModuleName DistroNexus -ParameterFilter { $Path -match "templates[\\/]test[\\/]install.sh" }
                Mock Get-Content { return "echo from path" } -ModuleName DistroNexus -ParameterFilter { $Path -match "templates[\\/]test[\\/]install.sh" }

                Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -Template $template

                Assert-MockCalled Get-Content -Times 1
            }
        }
    }
}
