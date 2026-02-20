Describe "Apply-DistroNexusTemplate Integration" -Tag 'Integration', 'Template' {
    BeforeAll {
        $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
        $modulePath = Join-Path $rootPath "src\PowerShell"
        Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    }

    It "Resolves and executes ScriptPath content" {
        InModuleScope DistroNexus {
            $template = [PSCustomObject]@{
                Id = "int-1"
                Name = "Integration ScriptPath"
                IsCustom = $false
                Scripts = @(
                    [PSCustomObject]@{
                        Name = "FromPath"
                        Type = "Bash"
                        ScriptPath = "templates/int/install.sh"
                        Order = 1
                    }
                )
            }

            Mock wsl.exe {
                $global:LASTEXITCODE = 0
                return "Ubuntu-22.04"
            } -ParameterFilter { $Args -contains "--list" }
            Mock wsl.exe {
                $global:LASTEXITCODE = 0
                return "ok"
            }
            Mock Test-Path { return $true } -ParameterFilter { $Path -match "templates[\\/]int[\\/]install.sh" }
            Mock Get-Content { return "echo integration" } -ParameterFilter { $Path -match "templates[\\/]int[\\/]install.sh" }

            Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -Template $template -Force

            Assert-MockCalled Get-Content -Times 1
            Assert-MockCalled wsl.exe -Times 2
        }
    }

    It "Rejects path traversal in ScriptPath" {
        InModuleScope DistroNexus {
            $template = [PSCustomObject]@{
                Id = "int-2"
                Name = "Traversal"
                IsCustom = $false
                Scripts = @(
                    [PSCustomObject]@{
                        Name = "Traversal"
                        Type = "Bash"
                        ScriptPath = "..\\evil.sh"
                        Order = 1
                    }
                )
            }

            Mock wsl.exe { return "Ubuntu-22.04" } -ParameterFilter { $Args -contains "--list" }
            Mock Write-Error { }

            Apply-DistroNexusTemplate -InstanceName "Ubuntu-22.04" -Template $template -Force

            Assert-MockCalled Write-Error -Times 1
        }
    }
}
