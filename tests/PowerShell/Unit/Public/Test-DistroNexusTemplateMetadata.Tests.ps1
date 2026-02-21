# Test-DistroNexusTemplateMetadata.Tests.ps1

Describe "Test-DistroNexusTemplateMetadata" -Tag 'Unit', 'Public' {

    BeforeAll {
        $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
        $modulePath = Join-Path $rootPath "src\PowerShell"
        Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    }

    It "Should pass lint for current repository templates metadata" {
        InModuleScope DistroNexus {
            $result = Test-DistroNexusTemplateMetadata

            $result.SchemaVersion | Should -Be '1.0'
            $result.Status | Should -Be 'Pass'
            $result.Summary.Errors | Should -Be 0
            $result.Violations.Count | Should -Be 0
        }
    }

    It "Should detect duplicate id, category and script path violations" {
        InModuleScope DistroNexus {
            $configPath = Join-Path $TestDrive 'templates.json'
            $templatesRoot = Join-Path $TestDrive 'templates\valid-template'
            [void](New-Item -Path $templatesRoot -ItemType Directory -Force)
            Set-Content -Path (Join-Path $templatesRoot 'install.sh') -Value '#!/usr/bin/env bash' -Encoding UTF8

            $invalidJson = @'
[
  {
    "Id": "dup-template",
    "Name": "Template A",
    "Category": "Development",
    "Description": "Valid baseline",
    "InstallMode": "Scripted",
    "Scripts": [
      {
        "Name": "Install A",
        "ScriptPath": "templates/valid-template/install.sh",
        "Type": "Bash",
        "Phase": "PostConfigure",
        "Order": 1,
        "TimeoutSeconds": 30
      }
    ]
  },
  {
    "Id": "dup-template",
    "Name": "Template B",
    "Category": "InvalidCategory",
    "Description": "Has invalid path",
    "InstallMode": "Scripted",
    "Scripts": [
      {
        "Name": "Install B",
        "ScriptPath": "../outside/install.sh",
        "Type": "Bash",
        "Phase": "PostConfigure",
        "Order": 1,
        "TimeoutSeconds": 30
      }
    ]
  }
]
'@

            Set-Content -Path $configPath -Value $invalidJson -Encoding UTF8

            $result = Test-DistroNexusTemplateMetadata -ConfigPath $configPath

            $result.Status | Should -Be 'Fail'
            $result.Summary.Errors | Should -BeGreaterThan 0
            @($result.Violations | Where-Object { $_.RuleId -eq 'metadata.template.duplicateId' }).Count | Should -Be 1
            @($result.Violations | Where-Object { $_.RuleId -eq 'metadata.template.categoryPolicy' }).Count | Should -Be 1
            @($result.Violations | Where-Object { $_.RuleId -eq 'metadata.script.pathSafety' }).Count | Should -BeGreaterThan 0
        }
    }

    It "Should output JSON report and throw in strict mode when errors exist" {
        InModuleScope DistroNexus {
            $configPath = Join-Path $TestDrive 'strict-templates.json'
            $reportPath = Join-Path $TestDrive 'lint\lint-report.json'

            $strictJson = @'
[
  {
    "Id": "strict-template",
    "Name": "Strict Template",
    "Category": "Development",
    "Description": "Invalid script reference",
    "InstallMode": "Scripted",
    "Scripts": [
      {
        "Name": "Install",
        "ScriptPath": "templates/strict-template/missing.sh",
        "Type": "Bash",
        "Phase": "PostConfigure",
        "Order": 1,
        "TimeoutSeconds": 30
      }
    ]
  }
]
'@

            Set-Content -Path $configPath -Value $strictJson -Encoding UTF8

            { Test-DistroNexusTemplateMetadata -ConfigPath $configPath -Strict -ReportPath $reportPath } | Should -Throw
            Test-Path $reportPath | Should -BeTrue

            $report = Get-Content -Path $reportPath -Raw | ConvertFrom-Json
            $report.SchemaVersion | Should -Be '1.0'
            $report.Status | Should -Be 'Fail'
            $report.Violations.Count | Should -BeGreaterThan 0
            $report.Violations[0].PSObject.Properties.Name -contains 'RuleId' | Should -BeTrue
            $report.Violations[0].PSObject.Properties.Name -contains 'Severity' | Should -BeTrue
            $report.Violations[0].PSObject.Properties.Name -contains 'Path' | Should -BeTrue
            $report.Violations[0].PSObject.Properties.Name -contains 'Message' | Should -BeTrue
        }
    }
}
