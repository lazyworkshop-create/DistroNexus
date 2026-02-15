# Get-DistroNexusTemplate.Tests.ps1

Describe "Get-DistroNexusTemplate" -Tag 'Unit', 'Public' {

    BeforeAll {
    # Import the module
    # Path: tests/PowerShell/Unit/Public -> Root
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
}
    
    Context "Template Loading" {
        
        It "Should return templates from config" {
            InModuleScope DistroNexus {
                # Mock Test-Path to simulate finding the config file
                Mock Test-Path { return $true } -ParameterFilter { $Path -match "templates.json" }

                # Mock content
                $jsonContent = '[
                    {"Id": "tpl-1", "Name": "Template 1", "Category": "Dev"},
                    {"Id": "tpl-2", "Name": "Template 2", "Category": "Ops"}
                ]'
                Mock Get-Content { return $jsonContent }

                $result = Get-DistroNexusTemplate
                
                $result.Count | Should -Be 2
                $result[0].Id | Should -Be "tpl-1"
            }
        }

        It "Should filter by Id" {
             InModuleScope DistroNexus {
                # Mock Test-Path to simulate finding the config file
                Mock Test-Path { return $true } -ParameterFilter { $Path -match "templates.json" }

                # Mock content
                $jsonContent = '[
                    {"Id": "tpl-1", "Name": "Template 1", "Category": "Dev"},
                    {"Id": "tpl-2", "Name": "Template 2", "Category": "Ops"}
                ]'
                Mock Get-Content { return $jsonContent }

                $result = Get-DistroNexusTemplate -Id "tpl-2"
                
                $result.Count | Should -Be 1
                $result.Name | Should -Be "Template 2"
             }
        }

        It "Should filter by Category" {
             InModuleScope DistroNexus {
                # Mock Test-Path to simulate finding the config file
                Mock Test-Path { return $true } -ParameterFilter { $Path -match "templates.json" }

                # Mock content
                $jsonContent = '[
                    {"Id": "tpl-1", "Name": "Template 1", "Category": "Dev"},
                    {"Id": "tpl-2", "Name": "Template 2", "Category": "Ops"}
                ]'
                Mock Get-Content { return $jsonContent }

                $result = Get-DistroNexusTemplate -Category "Dev"
                
                $result.Count | Should -Be 1
                $result.Id | Should -Be "tpl-1"
             }
        }
    }
}
