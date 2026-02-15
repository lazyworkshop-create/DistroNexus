# PackageHandler.Tests.ps1
# Unit tests for PackageHandler.ps1 Private functions

BeforeAll {
    # Import the module
    $rootPath = Resolve-Path "$PSScriptRoot/../../../.."
    $modulePath = Join-Path $rootPath "src\PowerShell"
    Import-Module (Join-Path $modulePath "DistroNexus.psd1") -Force
    
    # Import test helpers
    $helpersPath = Join-Path $PSScriptRoot "..\..\Helpers"
    . (Join-Path $helpersPath "MockHelpers.ps1")
    . (Join-Path $helpersPath "TestData.ps1")
}

Describe "Test-PackageFormat" -Tag 'Unit', 'PackageHandler' {
    BeforeEach {
        $global:testPackagePath = $TestDrive
    }
    
    Context "When testing supported formats" {
        It "Should return true for .tar file" {
            InModuleScope DistroNexus {
                # Arrange
                $tarFile = Join-Path $global:testPackagePath "ubuntu.tar"
                "mock content" | Out-File $tarFile
                
                # Act
                $result = Test-PackageFormat -Path $tarFile
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should return true for .tar.gz file" {
            InModuleScope DistroNexus {
                # Arrange
                $tarGzFile = Join-Path $global:testPackagePath "ubuntu.tar.gz"
                "mock content" | Out-File $tarGzFile
                
                # Act
                $result = Test-PackageFormat -Path $tarGzFile
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should return true for .tar.bz2 file" {
            InModuleScope DistroNexus {
                # Arrange
                $tarBz2File = Join-Path $global:testPackagePath "ubuntu.tar.bz2"
                "mock content" | Out-File $tarBz2File
                
                # Act
                $result = Test-PackageFormat -Path $tarBz2File
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should return true for .tar.xz file" {
            InModuleScope DistroNexus {
                # Arrange
                $tarXzFile = Join-Path $global:testPackagePath "ubuntu.tar.xz"
                "mock content" | Out-File $tarXzFile
                
                # Act
                $result = Test-PackageFormat -Path $tarXzFile
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should return true for .appx file" {
            InModuleScope DistroNexus {
                # Arrange
                $appxFile = Join-Path $global:testPackagePath "ubuntu.appx"
                "mock content" | Out-File $appxFile
                
                # Act
                $result = Test-PackageFormat -Path $appxFile
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should return true for .appxbundle file" {
            InModuleScope DistroNexus {
                # Arrange
                $appxBundleFile = Join-Path $global:testPackagePath "ubuntu.appxbundle"
                "mock content" | Out-File $appxBundleFile
                
                # Act
                $result = Test-PackageFormat -Path $appxBundleFile
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should return true for .zip file" {
            InModuleScope DistroNexus {
                # Arrange
                $zipFile = Join-Path $global:testPackagePath "ubuntu.zip"
                "mock content" | Out-File $zipFile
                
                # Act
                $result = Test-PackageFormat -Path $zipFile
                
                # Assert
                $result | Should -BeTrue
            }
        }
    }
    
    Context "When testing unsupported formats" {
        It "Should return false for .exe file" {
            InModuleScope DistroNexus {
                # Arrange
                $exeFile = Join-Path $global:testPackagePath "installer.exe"
                "mock content" | Out-File $exeFile
                
                # Act
                $result = Test-PackageFormat -Path $exeFile
                
                # Assert
                $result | Should -BeFalse
            }
        }
        
        It "Should return false for .txt file" {
            InModuleScope DistroNexus {
                # Arrange
                $txtFile = Join-Path $global:testPackagePath "readme.txt"
                "mock content" | Out-File $txtFile
                
                # Act
                $result = Test-PackageFormat -Path $txtFile
                
                # Assert
                $result | Should -BeFalse
            }
        }
        
        It "Should return false for non-existent file" {
            InModuleScope DistroNexus {
                # Arrange
                $nonExistentFile = Join-Path $global:testPackagePath "nonexistent.tar"
                
                # Act
                $result = Test-PackageFormat -Path $nonExistentFile
                
                # Assert
                $result | Should -BeFalse
            }
        }
    }
    
    Context "When testing case sensitivity" {
        It "Should handle uppercase extensions" {
            InModuleScope DistroNexus {
                # Arrange
                $upperFile = Join-Path $global:testPackagePath "UBUNTU.TAR.GZ"
                "mock content" | Out-File $upperFile
                
                # Act
                $result = Test-PackageFormat -Path $upperFile
                
                # Assert
                $result | Should -BeTrue
            }
        }
        
        It "Should handle mixed case extensions" {
            InModuleScope DistroNexus {
                # Arrange
                $mixedFile = Join-Path $global:testPackagePath "Ubuntu.Tar.Gz"
                "mock content" | Out-File $mixedFile
                
                # Act
                $result = Test-PackageFormat -Path $mixedFile
                
                # Assert
                $result | Should -BeTrue
            }
        }
    }
}

Describe "Get-PackageFormat" -Tag 'Unit', 'PackageHandler' {
    Context "When detecting package format" {
        It "Should detect TarGz format" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "ubuntu.tar.gz"
                
                # Assert
                $result | Should -Be 'TarGz'
            }
        }
        
        It "Should detect TarBz2 format" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "ubuntu.tar.bz2"
                
                # Assert
                $result | Should -Be 'TarBz2'
            }
        }
        
        It "Should detect TarXz format" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "ubuntu.tar.xz"
                
                # Assert
                $result | Should -Be 'TarXz'
            }
        }
        
        It "Should detect Tar format" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "ubuntu.tar"
                
                # Assert
                $result | Should -Be 'Tar'
            }
        }
        
        It "Should detect Appx format" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "ubuntu.appx"
                
                # Assert
                $result | Should -Be 'Appx'
            }
        }
        
        It "Should detect AppxBundle format" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "ubuntu.appxbundle"
                
                # Assert
                $result | Should -Be 'AppxBundle'
            }
        }
        
        It "Should detect Zip format" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "ubuntu.zip"
                
                # Assert
                $result | Should -Be 'Zip'
            }
        }
        
        It "Should return Unknown for unsupported format" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "ubuntu.exe"
                
                # Assert
                $result | Should -Be 'Unknown'
            }
        }
        
        It "Should handle case insensitive extensions" {
            InModuleScope DistroNexus {
                # Act
                $result = Get-PackageFormat -Path "UBUNTU.TAR.GZ"
                
                # Assert
                $result | Should -Be 'TarGz'
            }
        }
        
        It "Should prioritize double extensions over single" {
            InModuleScope DistroNexus {
                # Act - .tar.gz should be detected as TarGz, not Tar
                $result = Get-PackageFormat -Path "file.tar.gz"
                
                # Assert
                $result | Should -Be 'TarGz'
                $result | Should -Not -Be 'Tar'
            }
        }
    }
}

Describe "Expand-DistroPackage" -Tag 'Unit', 'PackageHandler' {
    BeforeEach {
        $global:testPackagePath = $TestDrive
    }
    
    Context "When expanding Tar format" {
        It "Should copy tar file when already in tar format" {
            InModuleScope DistroNexus {
                # Arrange
                $tarFile = Join-Path $global:testPackagePath "source.tar"
                $destination = Join-Path $global:testPackagePath "copied.tar"
                "mock tar content" | Out-File $tarFile

                # Act
                $result = Expand-DistroPackage -PackagePath $tarFile -DestinationPath $destination -Force

                # Assert
                Test-Path $destination | Should -BeTrue
                $result | Should -Be $destination
            }
        }
    }
    
    Context "When validating parameters" {
        It "Should throw when package file does not exist" {
            InModuleScope DistroNexus {
                # Arrange
                $nonExistentFile = Join-Path $global:testPackagePath "nonexistent.tar"
                
                # Act & Assert
                { Expand-DistroPackage -PackagePath $nonExistentFile } | Should -Throw
            }
        }
        
        It "Should warn when destination exists and Force not specified" {
            InModuleScope DistroNexus {
                # Arrange
                $tarFile = Join-Path $global:testPackagePath "test.tar"
                "mock content" | Out-File $tarFile
                
                $destFile = Join-Path $global:testPackagePath "test.tar"
                "existing content" | Out-File $destFile
                
                # Act
                $result = Expand-DistroPackage -PackagePath $tarFile -DestinationPath $destFile -WarningVariable warnings
                
                # Assert
                $warnings | Should -Not -BeNullOrEmpty
                $result | Should -Be $destFile
            }
        }
    }
    
    Context "When handling unsupported formats" {
        It "Should throw for unknown format" {
            InModuleScope DistroNexus {
                # Arrange
                $exeFile = Join-Path $global:testPackagePath "installer.exe"
                "mock content" | Out-File $exeFile
                
                # Act & Assert
                { Expand-DistroPackage -PackagePath $exeFile -WhatIf:$false } | Should -Throw "*Unsupported package format*"
            }
        }
    }
    
    Context "When using WhatIf parameter" {
        It "Should not actually expand package with WhatIf" {
            InModuleScope DistroNexus {
                # Arrange
                $tarFile = Join-Path $global:testPackagePath "test.tar"
                "mock content" | Out-File $tarFile
                
                $destFile = Join-Path $global:testPackagePath "output.tar"
                
                # Act
                Expand-DistroPackage -PackagePath $tarFile -DestinationPath $destFile -WhatIf
                
                # Assert - destination should not exist
                Test-Path $destFile | Should -BeFalse
            }
        }
    }
}

Describe "Test-TarCommand" -Tag 'Unit', 'PackageHandler' {
    Context "When checking tar command availability" {
        It "Should return boolean value" {
            InModuleScope DistroNexus {
                # Act
                $result = Test-TarCommand
                
                # Assert
                $result | Should -BeOfType [bool]
            }
        }
        
        It "Should return true on Windows 10/11 (tar is built-in)" {
            InModuleScope DistroNexus {
                # Act
                $result = Test-TarCommand
                
                # Assert - on modern Windows, tar should be available
                $result | Should -BeTrue
            }
        }
    }
}
