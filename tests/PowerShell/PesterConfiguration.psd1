@{
    Run = @{
        Path = @('Unit', 'Integration')
        PassThru = $true
        Exit = $false
    }
    CodeCoverage = @{
        Enabled = $true
        Path = @('../../src/PowerShell/Private/*.ps1', '../../src/PowerShell/Public/*.ps1')
        OutputFormat = 'CoverageGutters'
        OutputPath = '../../coverage/powershell-coverage.xml'
        OutputEncoding = 'UTF8'
    }
    TestResult = @{
        Enabled = $true
        OutputFormat = 'NUnitXml'
        OutputPath = '../../TestResults/powershell-results.xml'
    }
    Output = @{
        Verbosity = 'Detailed'
        StackTraceVerbosity = 'Full'
        CIFormat = 'Auto'
    }
    Should = @{
        ErrorAction = 'Stop'
    }
}
