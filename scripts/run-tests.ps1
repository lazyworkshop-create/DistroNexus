#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Local test runner script for WPF-PowerShell integration testing.

.DESCRIPTION
    Runs the complete test suite locally with options for:
    - Full integration tests
    - Quick unit tests only
    - Specific test categories
    - Coverage reporting

.PARAMETER TestType
    Type of tests to run: 'Full', 'Quick', 'PowerShell', 'CSharp'

.PARAMETER Coverage
    Generate code coverage report

.PARAMETER Verbose
    Verbose output from tests

.EXAMPLE
    .\run-tests.ps1 -TestType Full -Coverage
    
.EXAMPLE
    .\run-tests.ps1 -TestType Quick
#>

param(
    [ValidateSet('Full', 'Quick', 'PowerShell', 'CSharp', 'Integration')]
    [string]$TestType = 'Quick',
    
    [switch]$Coverage,
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

# Colors for output
$colors = @{
    Success = 'Green'
    Error = 'Red'
    Warning = 'Yellow'
    Info = 'Cyan'
}

function Write-Result {
    param([string]$Message, [string]$Type = 'Info')
    Write-Host $Message -ForegroundColor $colors[$Type]
}

# Create test results directory
$testResultsDir = Join-Path (Get-Item -Path $PSScriptRoot).Parent.FullName 'test-results'
if (-not (Test-Path $testResultsDir)) {
    New-Item -ItemType Directory -Path $testResultsDir -Force | Out-Null
}

Write-Result "═══════════════════════════════════════════════════════════" -Type Info
Write-Result "DistroNexus Integration Test Runner" -Type Info
Write-Result "Test Type: $TestType | Coverage: $Coverage | Verbose: $Verbose" -Type Info
Write-Result "═══════════════════════════════════════════════════════════" -Type Info
Write-Result ""

# Check prerequisites
Write-Result "Checking prerequisites..." -Type Info

# Check PowerShell version
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -lt 7) {
    Write-Result "Warning: PowerShell 7+ recommended (current: $psVersion)" -Type Warning
}

# Check dotnet
try {
    $dotnetVersion = dotnet --version
    Write-Result "✓ .NET SDK: $dotnetVersion" -Type Success
} catch {
    Write-Result "✗ .NET SDK not found. Please install .NET 8.0 or later." -Type Error
    exit 1
}

# Check Pester
try {
    $pesterModule = Get-Module Pester -ListAvailable | Select-Object -First 1
    if ($pesterModule.Version.Major -ge 5) {
        Write-Result "✓ Pester: $($pesterModule.Version)" -Type Success
    } else {
        Write-Result "⚠ Pester version $($pesterModule.Version) detected. Version 5.0+ recommended." -Type Warning
    }
} catch {
    Write-Result "ℹ Pester will be installed automatically" -Type Info
}

Write-Result ""

# Run tests based on type
$testStopwatch = [Diagnostics.Stopwatch]::StartNew()

switch ($TestType) {
    'PowerShell' {
        Write-Result "Running PowerShell Tests..." -Type Info
        
        # Install Pester if needed
        if (-not (Get-Module Pester -ListAvailable | Where-Object { $_.Version.Major -ge 5 })) {
            Write-Result "Installing Pester..." -Type Info
            Install-Module -Name Pester -Repository PSGallery -Force -SkipPublisherCheck | Out-Null
        }
        
        # Run unit tests
        Write-Result ""
        Write-Result "Running PowerShell Unit Tests..." -Type Info
        $unitTestConfig = @{
            Run = @{
                Path = './tests/PowerShell/Unit'
            }
            TestResult = @{
                Enabled = $true
                OutputPath = "$testResultsDir/pester-unit-results.xml"
                OutputFormat = 'NUnitXml'
            }
            Output = @{
                Verbosity = if ($Verbose) { 'Detailed' } else { 'Normal' }
            }
        }
        
        if ($Coverage) {
            $unitTestConfig.CodeCoverage = @{
                Enabled = $true
                OutputPath = "$testResultsDir/pester-unit-coverage.xml"
                OutputFormat = 'CoverageGutters'
                Path = './src/PowerShell/Public', './src/PowerShell/Private'
            }
        }
        
        $unitResult = Invoke-Pester -Configuration $unitTestConfig
        
        if ($unitResult.FailedCount -gt 0) {
            Write-Result "PowerShell Unit Tests Failed" -Type Error
        } else {
            Write-Result "✓ PowerShell Unit Tests Passed ($($unitResult.PassedCount) tests)" -Type Success
        }
        
        # Run integration tests
        Write-Result ""
        Write-Result "Running PowerShell Integration Tests..." -Type Info
        $integrationTestConfig = @{
            Run = @{
                Path = './tests/PowerShell/Integration'
            }
            TestResult = @{
                Enabled = $true
                OutputPath = "$testResultsDir/pester-integration-results.xml"
                OutputFormat = 'NUnitXml'
            }
            Output = @{
                Verbosity = if ($Verbose) { 'Detailed' } else { 'Normal' }
            }
        }
        
        if ($Coverage) {
            $integrationTestConfig.CodeCoverage = @{
                Enabled = $true
                OutputPath = "$testResultsDir/pester-integration-coverage.xml"
                OutputFormat = 'CoverageGutters'
                Path = './src/PowerShell'
            }
        }
        
        $integrationResult = Invoke-Pester -Configuration $integrationTestConfig
        
        if ($integrationResult.FailedCount -gt 0) {
            Write-Result "PowerShell Integration Tests Failed" -Type Error
        } else {
            Write-Result "✓ PowerShell Integration Tests Passed ($($integrationResult.PassedCount) tests)" -Type Success
        }
    }
    
    'CSharp' {
        Write-Result "Running C# Tests..." -Type Info
        Write-Result ""
        
        # Build
        Write-Result "Building C# solution..." -Type Info
        $buildResult = dotnet build src/Client/ --configuration Debug
        if ($LASTEXITCODE -ne 0) {
            Write-Result "✗ Build failed" -Type Error
            exit 1
        }
        Write-Result "✓ Build succeeded" -Type Success
        
        # Run tests
        Write-Result ""
        Write-Result "Running C# unit and integration tests..." -Type Info
        
        $testArgs = @(
            'test'
            'src/Client/DistroNexus.Tests/'
            '--configuration', 'Debug'
            '--no-build'
            '--logger', "trx;LogFileName=$testResultsDir/csharp-results.trx"
        )
        
        if ($Verbose) {
            $testArgs += '--verbosity', 'detailed'
        } else {
            $testArgs += '--verbosity', 'normal'
        }
        
        if ($Coverage) {
            $testArgs += @(
                '--collect:XPlat Code Coverage'
                '/p:CoverletOutput=' + $testResultsDir
                '/p:CoverletOutputFormat=cobertura'
            )
        }
        
        & dotnet $testArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Result "✓ C# Tests Passed" -Type Success
        } else {
            Write-Result "✗ C# Tests Failed" -Type Error
        }
    }
    
    'Quick' {
        Write-Result "Running Quick Test Suite (Unit Tests Only)..." -Type Info
        
        # Install Pester if needed
        if (-not (Get-Module Pester -ListAvailable | Where-Object { $_.Version.Major -ge 5 })) {
            Write-Result "Installing Pester..." -Type Info
            Install-Module -Name Pester -Repository PSGallery -Force -SkipPublisherCheck | Out-Null
        }
        
        # PowerShell quick tests
        Write-Result ""
        Write-Result "Running PowerShell unit tests..." -Type Info
        $quickPsTestConfig = @{
            Run = @{
                Path = './tests/PowerShell/Unit'
            }
            TestResult = @{
                Enabled = $true
                OutputPath = "$testResultsDir/quick-pester-results.xml"
                OutputFormat = 'NUnitXml'
            }
            Output = @{
                Verbosity = if ($Verbose) { 'Detailed' } else { 'Minimal' }
            }
        }
        
        $psResult = Invoke-Pester -Configuration $quickPsTestConfig
        
        # C# quick tests
        Write-Result ""
        Write-Result "Building and testing C# code..." -Type Info
        
        $buildResult = dotnet build src/Client/ --configuration Debug -q
        if ($LASTEXITCODE -ne 0) {
            Write-Result "✗ Build failed" -Type Error
            exit 1
        }
        
        $testArgs = @(
            'test'
            'src/Client/DistroNexus.Tests/'
            '--configuration', 'Debug'
            '--no-build'
            '--filter', 'Category!=Integration'
            '--logger', "trx;LogFileName=$testResultsDir/quick-csharp-results.trx"
            '-q'
        )
        
        & dotnet $testArgs
        $csharpSuccess = $LASTEXITCODE -eq 0
        
        # Results
        Write-Result ""
        Write-Result "Quick Test Results:" -Type Info
        if ($psResult.FailedCount -eq 0 -and $csharpSuccess) {
            Write-Result "✓ All quick tests passed!" -Type Success
        } else {
            Write-Result "✗ Some tests failed" -Type Error
            if ($psResult.FailedCount -gt 0) {
                Write-Result "  - PowerShell: $($psResult.FailedCount) failures" -Type Error
            }
            if (-not $csharpSuccess) {
                Write-Result "  - C#: Check above for details" -Type Error
            }
        }
    }
    
    'Full' {
        Write-Result "Running Full Test Suite (All Tests)..." -Type Info
        
        # PowerShell tests
        Write-Result ""
        Write-Result "═══════════════════════════════════════════════════════════" -Type Info
        Write-Result "PowerShell Tests" -Type Info
        Write-Result "═══════════════════════════════════════════════════════════" -Type Info
        
        if (-not (Get-Module Pester -ListAvailable | Where-Object { $_.Version.Major -ge 5 })) {
            Install-Module -Name Pester -Repository PSGallery -Force -SkipPublisherCheck | Out-Null
        }
        
        $psUnitConfig = @{
            Run = @{ Path = './tests/PowerShell/Unit' }
            TestResult = @{
                Enabled = $true
                OutputPath = "$testResultsDir/pester-unit-results.xml"
                OutputFormat = 'NUnitXml'
            }
            Output = @{ Verbosity = if ($Verbose) { 'Detailed' } else { 'Normal' } }
        }
        $psIntegrationConfig = @{
            Run = @{ Path = './tests/PowerShell/Integration' }
            TestResult = @{
                Enabled = $true
                OutputPath = "$testResultsDir/pester-integration-results.xml"
                OutputFormat = 'NUnitXml'
            }
            Output = @{ Verbosity = if ($Verbose) { 'Detailed' } else { 'Normal' } }
        }
        
        if ($Coverage) {
            $psUnitConfig.CodeCoverage = @{
                Enabled = $true
                OutputPath = "$testResultsDir/pester-unit-coverage.xml"
                OutputFormat = 'CoverageGutters'
                Path = './src/PowerShell/Public', './src/PowerShell/Private'
            }
            $psIntegrationConfig.CodeCoverage = @{
                Enabled = $true
                OutputPath = "$testResultsDir/pester-integration-coverage.xml"
                OutputFormat = 'CoverageGutters'
                Path = './src/PowerShell'
            }
        }
        
        Invoke-Pester -Configuration $psUnitConfig
        Invoke-Pester -Configuration $psIntegrationConfig
        
        # C# tests
        Write-Result ""
        Write-Result "═══════════════════════════════════════════════════════════" -Type Info
        Write-Result "C# Tests" -Type Info
        Write-Result "═══════════════════════════════════════════════════════════" -Type Info
        
        dotnet build src/Client/ --configuration Release -q
        
        $testArgs = @(
            'test'
            'src/Client/DistroNexus.Tests/'
            '--configuration', 'Release'
            '--no-build'
            '--logger', "trx;LogFileName=$testResultsDir/csharp-results.trx"
            '-v', if ($Verbose) { 'normal' } else { 'quiet' }
        )
        
        if ($Coverage) {
            $testArgs += @(
                '--collect:XPlat Code Coverage'
                '/p:CoverletOutput=' + $testResultsDir
                '/p:CoverletOutputFormat=cobertura'
            )
        }
        
        & dotnet $testArgs
    }
}

$testStopwatch.Stop()
Write-Result ""
Write-Result "═══════════════════════════════════════════════════════════" -Type Info
Write-Result "Test execution completed in $($testStopwatch.Elapsed.TotalSeconds)s" -Type Info
Write-Result "Results saved to: $testResultsDir" -Type Success
Write-Result "═══════════════════════════════════════════════════════════" -Type Info
