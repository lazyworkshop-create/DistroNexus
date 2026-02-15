# DistroNexus PowerShell Module Test Runner
# This script runs all Pester tests with proper configuration

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Unit', 'Integration', 'All')]
    [string]$TestType = 'All',
    
    [Parameter()]
    [switch]$CodeCoverage,
    
    [Parameter()]
    [switch]$CI,

    [Parameter()]
    [switch]$EnableWsl2Scenarios,
    
    [Parameter()]
    [string]$OutputPath = '../../TestResults'
)

# Ensure Pester is available
$pesterModule = Get-Module -Name Pester -ListAvailable | Where-Object { $_.Version -ge '5.0.0' } | Select-Object -First 1
if (-not $pesterModule) {
    Write-Error "Pester 5.x or higher is required. Install it using: Install-Module -Name Pester -MinimumVersion 5.0.0 -Force"
    exit 1
}

Import-Module Pester -MinimumVersion 5.0.0 -Force

# Configure test paths
$testRoot = $PSScriptRoot
$projectRoot = Split-Path (Split-Path $testRoot -Parent) -Parent
$sourceRoot = Join-Path $projectRoot 'src\PowerShell'

Write-Host "Test Root: $testRoot" -ForegroundColor Cyan
Write-Host "Project Root: $projectRoot" -ForegroundColor Cyan
Write-Host "Source Root: $sourceRoot" -ForegroundColor Cyan

# Create output directories
$outputDir = Join-Path $projectRoot $OutputPath
$coverageDir = Join-Path $projectRoot 'coverage'
New-Item -Path $outputDir -ItemType Directory -Force | Out-Null
New-Item -Path $coverageDir -ItemType Directory -Force | Out-Null

# Configure Pester
$config = New-PesterConfiguration

# Determine which tests to run
switch ($TestType) {
    'Unit' { 
        $config.Run.Path = Join-Path $testRoot 'Unit'
    }
    'Integration' { 
        $config.Run.Path = Join-Path $testRoot 'Integration'
    }
    'All' { 
        $config.Run.Path = @(
            (Join-Path $testRoot 'Unit'),
            (Join-Path $testRoot 'Integration')
        )
    }
}

$config.Run.PassThru = $true
$config.Run.Exit = $false

# Output configuration
$config.Output.Verbosity = if ($CI) { 'Detailed' } else { 'Normal' }
$config.Output.StackTraceVerbosity = 'Full'
$config.Output.CIFormat = 'Auto'

# Test results
$config.TestResult.Enabled = $true
$config.TestResult.OutputFormat = 'NUnitXml'
$config.TestResult.OutputPath = Join-Path $outputDir 'powershell-results.xml'

# Code coverage
if ($CodeCoverage) {
    $config.CodeCoverage.Enabled = $true
    $config.CodeCoverage.Path = @(
        (Join-Path $sourceRoot 'Private\*.ps1'),
        (Join-Path $sourceRoot 'Public\*.ps1')
    )
    $config.CodeCoverage.OutputFormat = 'CoverageGutters'
    $config.CodeCoverage.OutputPath = Join-Path $coverageDir 'powershell-coverage.xml'
    $config.CodeCoverage.OutputEncoding = 'UTF8'
    
    Write-Host "Code coverage enabled. Output: $($config.CodeCoverage.OutputPath)" -ForegroundColor Green
}

# Should configuration
$config.Should.ErrorAction = 'Stop'

# Run tests
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Running DistroNexus PowerShell Tests" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan
Write-Host "Test Type: $TestType" -ForegroundColor Yellow
Write-Host "Code Coverage: $($CodeCoverage.IsPresent)" -ForegroundColor Yellow
Write-Host "CI Mode: $($CI.IsPresent)" -ForegroundColor Yellow

if ($EnableWsl2Scenarios) {
    $env:DISTRONEXUS_RUN_WSL2_TESTS = '1'
    Write-Host "WSL2 Scenarios: Enabled`n" -ForegroundColor Yellow
}
else {
    Remove-Item Env:DISTRONEXUS_RUN_WSL2_TESTS -ErrorAction SilentlyContinue
    Write-Host "WSL2 Scenarios: Disabled`n" -ForegroundColor Yellow
}

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$result = Invoke-Pester -Configuration $config
$stopwatch.Stop()

# Display results
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Test Results Summary" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Total Tests:   $($result.TotalCount)" -ForegroundColor White
Write-Host "Passed:        $($result.PassedCount)" -ForegroundColor Green
Write-Host "Failed:        $($result.FailedCount)" -ForegroundColor $(if ($result.FailedCount -gt 0) { 'Red' } else { 'Green' })
Write-Host "Skipped:       $($result.SkippedCount)" -ForegroundColor Yellow
Write-Host "Duration:      $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))`n" -ForegroundColor White

if ($CodeCoverage -and $result.CodeCoverage) {
    $coverage = $result.CodeCoverage
    $coveredPercent = if ($coverage.CommandsAnalyzedCount -gt 0) {
        [Math]::Round(($coverage.CommandsExecutedCount / $coverage.CommandsAnalyzedCount) * 100, 2)
    } else { 0 }
    
    Write-Host "Code Coverage:" -ForegroundColor Cyan
    Write-Host "  Commands Analyzed:  $($coverage.CommandsAnalyzedCount)" -ForegroundColor White
    Write-Host "  Commands Executed:  $($coverage.CommandsExecutedCount)" -ForegroundColor White
    Write-Host "  Coverage:           $coveredPercent%" -ForegroundColor $(if ($coveredPercent -ge 70) { 'Green' } else { 'Yellow' })
    Write-Host ""
}

# Display failed tests details
if ($result.FailedCount -gt 0) {
    Write-Host "Failed Tests:" -ForegroundColor Red
    foreach ($test in $result.Failed) {
        Write-Host "  - $($test.ExpandedName)" -ForegroundColor Red
        Write-Host "    Error: $($test.ErrorRecord.Exception.Message)" -ForegroundColor DarkRed
    }
    Write-Host ""
}

# Exit with appropriate code
if ($result.FailedCount -gt 0) {
    Write-Error "Tests failed! $($result.FailedCount) test(s) did not pass."
    exit 1
} else {
    Write-Host "All tests passed successfully! ✓" -ForegroundColor Green
    exit 0
}
