BeforeAll {
    . "$PSScriptRoot/../../../../src/PowerShell/Private/Logger.ps1"
    Initialize-DistroNexusLogger -LogFileName "test-logger.log"
}

Describe "Write-DistroNexusLog -ErrorCode" {
    It "accepts -ErrorCode parameter without error" {
        { Write-DistroNexusLog "test message" -Level ERROR -ErrorCode "DN-1001" -FileOnly } |
            Should -Not -Throw
    }

    It "includes error code in log line when provided" {
        Write-DistroNexusLog "coded error" -Level ERROR -ErrorCode "DN-2003" -FileOnly
        $lastLine = Get-Content $script:LogFile | Select-Object -Last 1
        $lastLine | Should -Match "DN-2003"
    }
}
