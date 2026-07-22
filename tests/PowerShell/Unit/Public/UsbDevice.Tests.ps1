BeforeAll {
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    Import-Module (Join-Path $rootPath 'src/PowerShell/DistroNexus.psd1') -Force
}

Describe 'DistroNexus USB cmdlets' -Tag 'Unit', 'Public', 'Usb' {
    It 'exports the read and constrained mutation commands' {
        Get-Command Get-DistroNexusUsbDevice, Connect-DistroNexusUsbDevice, Disconnect-DistroNexusUsbDevice | Should -HaveCount 3
    }
    It 'runs a structured preflight and returns its preview for WhatIf attach without mutation' {
        InModuleScope DistroNexus {
            $preview = [PSCustomObject]@{ PSTypeName = 'DistroNexus.UsbDeviceActionPreview'; Action = 'Attach'; BusId = '1-2'; Distribution = 'Ubuntu'; RequiresConfirmation = $true; Effects = @('fixture'); Warnings = @('fixture') }
            Mock Get-DistroNexusUsbActionPreflight { [PSCustomObject]@{ Succeeded = $true; Preview = $preview } }
            Mock Get-DistroNexusUsbIpdStatus { throw 'usbipd mutation must not run for WhatIf' }
            $result = @(Connect-DistroNexusUsbDevice -BusId '1-2' -Distribution Ubuntu -WhatIf)
            $result | Should -HaveCount 1
            $result[0].PSTypeNames | Should -Contain 'DistroNexus.UsbDeviceActionPreview'
            Assert-MockCalled Get-DistroNexusUsbActionPreflight -Times 1 -Exactly
            Assert-MockCalled Get-DistroNexusUsbIpdStatus -Times 0 -Exactly
        }
    }
    It 'rejects command-like bus IDs before any operation' {
        { Disconnect-DistroNexusUsbDevice -BusId '1-2;whoami' -WhatIf } | Should -Throw
    }
    It 'rejects a v5-only bus_id fixture for the v4 parser' {
        InModuleScope DistroNexus {
            $row = [PSCustomObject]@{ bus_id = '1-2'; vidPid = '1234:5678'; device = 'Fixture'; status = 'Shared' }
            ConvertTo-DistroNexusUsbDevice -Row $row -Major 4 | Should -BeNullOrEmpty
        }
    }
    It 'accepts a v5 bus_id fixture using its documented v5 fields' {
        InModuleScope DistroNexus {
            $row = [PSCustomObject]@{ bus_id = '1-a'; vidPid = '1234:5678'; device = 'Fixture'; status = 'Shared' }
            $device = ConvertTo-DistroNexusUsbDevice -Row $row -Major 5
            $device.BusId | Should -Be '1-A'
            $device.HardwareId | Should -Be '1234:5678'
            $device.Description | Should -Be 'Fixture'
            $device.State | Should -Be 'Shared'
        }
    }
    It 'rejects mixed per-major fields and malformed hardware identities from JSON rows' {
        InModuleScope DistroNexus {
            $mixedV5 = [PSCustomObject]@{ busId = '1-2'; vidPid = '1234:5678'; device = 'Fixture'; status = 'Shared' }
            $badV4 = [PSCustomObject]@{ busId = '1-2'; hardwareId = '1234:567'; description = 'Fixture'; state = 'Shared' }
            $badV5 = [PSCustomObject]@{ bus_id = '1-2'; vidPid = ('A' * 5000) + ':5678'; device = 'Fixture'; status = 'Shared' }
            ConvertTo-DistroNexusUsbDevice -Row $mixedV5 -Major 5 | Should -BeNullOrEmpty
            ConvertTo-DistroNexusUsbDevice -Row $badV4 -Major 4 | Should -BeNullOrEmpty
            ConvertTo-DistroNexusUsbDevice -Row $badV5 -Major 5 | Should -BeNullOrEmpty
        }
    }
    It 'parses a supported usbipd version from a real command fixture' {
        InModuleScope DistroNexus {
            $fixture = Join-Path $TestDrive 'usbipd-fixture.ps1'
            Set-Content -LiteralPath $fixture -Value "param([string]`$Argument); if (`$Argument -eq '--version') { 'usbipd-win 5.1.2' }"
            Mock Get-Command { [PSCustomObject]@{ Source = $fixture } } -ParameterFilter { $Name -eq 'usbipd.exe' }
            $status = Get-DistroNexusUsbIpdStatus
            $status.Installed | Should -BeTrue
            $status.Major | Should -Be 5
            $status.Version | Should -Be '5.1.2'
        }
    }
    It 'does not inherit a stale native failure from an earlier fixture command' {
        InModuleScope DistroNexus {
            $global:LASTEXITCODE = 99
            $fixture = Join-Path $TestDrive 'usbipd-stale-exit.ps1'
            Set-Content -LiteralPath $fixture -Value "param([string]`$Argument); if (`$Argument -eq '--version') { 'usbipd-win 4.2.0' }"
            Mock Get-Command { [PSCustomObject]@{ Source = $fixture } } -ParameterFilter { $Name -eq 'usbipd.exe' }
            Mock Invoke-DistroNexusUsbNative -ParameterFilter { $FilePath -eq 'sc.exe' } { [PSCustomObject]@{ Output = @('STATE : 4 RUNNING'); ExitCode = 0 } }
            $status = Get-DistroNexusUsbIpdStatus
            $status.Major | Should -Be 4
        }
    }
    It 'bounds oversized version fixture components and fails mutation closed' {
        InModuleScope DistroNexus {
            $fixture = Join-Path $TestDrive 'usbipd-large-version.ps1'
            Set-Content -LiteralPath $fixture -Value "param([string]`$Argument); if (`$Argument -eq '--version') { 'usbipd-win ' + ('9' * 5000) + '.1' }"
            Mock Get-Command { [PSCustomObject]@{ Source = $fixture } } -ParameterFilter { $Name -eq 'usbipd.exe' }
            Mock Invoke-DistroNexusUsbNative -ParameterFilter { $FilePath -eq 'sc.exe' } { [PSCustomObject]@{ Output = @('STATE : 4 RUNNING'); ExitCode = 0 } }
            $status = Get-DistroNexusUsbIpdStatus
            $status.Major | Should -BeNullOrEmpty
            $status.SupportsMutation | Should -BeFalse
        }
    }
    It 'parses documented usbipd list table rows and ignores malformed rows' {
        InModuleScope DistroNexus {
            $fixture = Join-Path $TestDrive 'usbipd-list-fixture.ps1'
            Set-Content -LiteralPath $fixture -Value "param([string]`$Argument); if (`$Argument -eq 'list') { '1-a    2341:0043   Arduino Uno  Shared'; 'bad ;  0000:0000   ignored  Shared' }"
            $status = [PSCustomObject]@{ Major = 99; Command = [PSCustomObject]@{ Source = $fixture } }
            $devices = @(Get-DistroNexusUsbDeviceRows -Status $status)
            $devices | Should -HaveCount 1
            $devices[0].BusId | Should -Be '1-A'
            $devices[0].HardwareId | Should -Be '2341:0043'
            $devices[0].State | Should -Be 'Shared'
        }
    }
    It 'falls back to the table contract when an approved JSON response has a cross-major row' {
        InModuleScope DistroNexus {
            $status = [PSCustomObject]@{ Major = 4; Command = [PSCustomObject]@{ Source = 'fixture' } }
            Mock Invoke-DistroNexusUsbNative {
                if ($ArgumentList -contains '--json') {
                    [PSCustomObject]@{ Output = @('{"devices":[{"bus_id":"1-2","vidPid":"2341:0043","device":"Fixture","status":"Shared"}]}'); ExitCode = 0 }
                } else {
                    [PSCustomObject]@{ Output = @('1-2  2341:0043  Table Fixture  Shared'); ExitCode = 0 }
                }
            }
            $device = @(Get-DistroNexusUsbDeviceRows -Status $status)
            $device | Should -HaveCount 1
            $device[0].Description | Should -Be 'Table Fixture'
        }
    }
    It 'fails attach preflight before ShouldProcess when the device state is not shared' {
        InModuleScope DistroNexus {
            Mock Get-DistroNexusUsbIpdStatus { [PSCustomObject]@{ Installed = $true; ServiceRunning = $true; SupportsMutation = $true; Major = 5; Command = [PSCustomObject]@{ Source = 'fixture' } } }
            Mock Get-DistroNexusUsbDeviceRows { [PSCustomObject]@{ BusId = '1-2'; State = 'Available' } }
            $result = Get-DistroNexusUsbActionPreflight -Action Attach -BusId '1-2' -Distribution Ubuntu
            $result.Succeeded | Should -BeFalse
            $result.ErrorId | Should -Be 'DistroNexus.Usb.StateChanged'
        }
    }
    It 'binds the one-time preview to hardware identity and rejects a reused bus ID' {
        InModuleScope DistroNexus {
            $script:fixtureDevice = [PSCustomObject]@{ BusId = '1-2'; HardwareId = '2341:0043'; State = 'Shared' }
            Mock Get-DistroNexusUsbIpdStatus { [PSCustomObject]@{ Installed = $true; ServiceRunning = $true; SupportsMutation = $true; Major = 5; Command = [PSCustomObject]@{ Source = 'fixture' } } }
            Mock Get-DistroNexusUsbDeviceRows { $script:fixtureDevice }
            $first = Get-DistroNexusUsbActionPreflight -Action Attach -BusId '1-2' -Distribution Ubuntu
            $script:fixtureDevice = [PSCustomObject]@{ BusId = '1-2'; HardwareId = '9999:0001'; State = 'Shared' }
            $second = Get-DistroNexusUsbActionPreflight -Action Attach -BusId '1-2' -Distribution Ubuntu -PreviewToken $first.Preview.Token
            $second.Succeeded | Should -BeFalse
            $second.ErrorId | Should -Be 'DistroNexus.Usb.PreviewRequired'
        }
    }
    It 'rejects state substrings and case aliases from JSON rows' {
        InModuleScope DistroNexus {
            foreach ($state in @('Attached elsewhere', 'Shared-ish', 'not shared')) {
                $row = [PSCustomObject]@{ busId = '1-2'; hardwareId = '2341:0043'; description = 'Fixture'; state = $state }
                ConvertTo-DistroNexusUsbDevice -Row $row -Major 4 | Should -BeNullOrEmpty
            }
        }
    }
}
