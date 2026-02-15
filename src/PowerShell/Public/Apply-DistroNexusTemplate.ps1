function Apply-DistroNexusTemplate {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
    param (
        [Parameter(Mandatory = $true)]
        [string]$InstanceName,

        [Parameter(Mandatory = $true, ParameterSetName = 'ById')]
        [string]$TemplateId,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByObject', ValueFromPipeline = $true)]
        [PSCustomObject]$Template,

        [hashtable]$Variables,

        [switch]$Force
    )

    process {
        if ($PSCmdlet.ParameterSetName -eq 'ById') {
            $Template = Get-DistroNexusTemplate -Id $TemplateId
            if (-not $Template) {
                Write-Error "Template '$TemplateId' not found."
                return
            }
        }

        Write-Verbose "Applying template '$($Template.Name)' to instance '$InstanceName'..."

        if ($Template.PSObject.Properties.Name -contains 'IsCustom' -and $Template.IsCustom -and -not $Force) {
            if (-not $PSCmdlet.ShouldContinue("Custom template '$($Template.Name)' may execute untrusted scripts. Continue?", "Confirm Custom Template")) {
                Write-Warning "Custom template application cancelled by user."
                return
            }
        }

        # Check if instance exists
        $wslList = (wsl.exe --list --quiet)
        # Handle UTF-16 output issue of wsl --list --quiet if needed, or just standard string check
        # Assuming standard output
        if ($wslList -notmatch $InstanceName) {
            # This is a weak check, but suffice for now
             Write-Verbose "Instance listing: $wslList"
        }

        $scripts = $Template.Scripts | Sort-Object Order
        $count = 0
        $total = $scripts.Count

        if ($total -eq 0) {
            Write-Verbose "Template has no scripts to execute."
            return
        }

        function Get-ScriptContent {
            param(
                [Parameter(Mandatory = $true)]
                [PSCustomObject]$Script
            )

            if (-not [string]::IsNullOrWhiteSpace($Script.Content)) {
                return [string]$Script.Content
            }

            if ([string]::IsNullOrWhiteSpace($Script.ScriptPath)) {
                return $null
            }

            $candidatePaths = @()
            if ([System.IO.Path]::IsPathRooted($Script.ScriptPath)) {
                throw "Absolute script path is not allowed: $($Script.ScriptPath)"
            } else {
                $candidatePaths += (Join-Path $script:ProjectRoot (Join-Path "config" $Script.ScriptPath))
            }

            $allowedRoots = @(
                [System.IO.Path]::GetFullPath((Join-Path $script:ProjectRoot "config"))
            )

            foreach ($path in $candidatePaths) {
                $fullPath = [System.IO.Path]::GetFullPath($path)
                $isAllowed = $false
                foreach ($root in $allowedRoots) {
                    if ($fullPath.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
                        $isAllowed = $true
                        break
                    }
                }

                if (-not $isAllowed) {
                    throw "Script path traversal detected: $($Script.ScriptPath)"
                }

                if (Test-Path $fullPath) {
                    return Get-Content -Path $fullPath -Raw
                }
            }

            throw "Script file not found for '$($Script.Name)': $($Script.ScriptPath)"
        }

        function Resolve-ScriptFilePath {
            param(
                [Parameter(Mandatory = $true)]
                [PSCustomObject]$Script
            )

            if ([string]::IsNullOrWhiteSpace($Script.ScriptPath)) {
                return $null
            }

            if ([System.IO.Path]::IsPathRooted($Script.ScriptPath)) {
                throw "Absolute script path is not allowed: $($Script.ScriptPath)"
            }

            $candidatePath = Join-Path $script:ProjectRoot (Join-Path "config" $Script.ScriptPath)
            $fullPath = [System.IO.Path]::GetFullPath($candidatePath)
            $allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $script:ProjectRoot "config"))

            if (-not $fullPath.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Script path traversal detected: $($Script.ScriptPath)"
            }

            if (-not (Test-Path $fullPath)) {
                throw "Script file not found for '$($Script.Name)': $($Script.ScriptPath)"
            }

            return $fullPath
        }

        function Convert-WindowsPathToWslPath {
            param(
                [Parameter(Mandatory = $true)]
                [string]$WindowsPath
            )

            $normalizedPath = [System.IO.Path]::GetFullPath($WindowsPath)
            $driveRoot = [System.IO.Path]::GetPathRoot($normalizedPath)
            if ([string]::IsNullOrWhiteSpace($driveRoot) -or $driveRoot.Length -lt 2) {
                throw "Unable to convert path to WSL path: $WindowsPath"
            }

            $driveLetter = $driveRoot.Substring(0, 1).ToLowerInvariant()
            $tail = $normalizedPath.Substring($driveRoot.Length).Replace('\', '/')
            return "/mnt/$driveLetter/$tail"
        }

        function Apply-TemplateVariables {
            param(
                [string]$Content,
                [hashtable]$VariableMap
            )

            if ([string]::IsNullOrWhiteSpace($Content) -or -not $VariableMap) {
                return $Content
            }

            foreach ($key in $VariableMap.Keys) {
                $Content = $Content.Replace("`${$key}", [string]$VariableMap[$key])
            }

            return $Content
        }

        foreach ($script in $scripts) {
             $count++
             $percent = ($count / $total) * 100
             Write-Progress -Activity "Applying Template $($Template.Name)" -Status "Executing $($script.Name)" -PercentComplete $percent
             
             try {
                $scriptFilePath = Resolve-ScriptFilePath -Script $script
                $content = Get-ScriptContent -Script $script

                if ($Template.PSObject.Properties.Name -contains 'Variables' -and $Template.Variables) {
                    $content = Apply-TemplateVariables -Content $content -VariableMap $Template.Variables
                }

                if ($Variables) {
                    $content = Apply-TemplateVariables -Content $content -VariableMap $Variables
                }

                $hasTemplateVariables = ($Template.PSObject.Properties.Name -contains 'Variables' -and $Template.Variables)
                $hasRuntimeVariables = ($null -ne $Variables -and $Variables.Count -gt 0)
                $hasVariableInjection = ($hasTemplateVariables -or $hasRuntimeVariables)

                if ($script.Type -eq 'Bash' -or $script.Type -eq 0) { # 0 is Bash enum value
                     if ([string]::IsNullOrWhiteSpace($content)) {
                         Write-Warning "Skipping empty script $($script.Name)"
                         continue
                     }

                     if ($scriptFilePath -and -not $hasVariableInjection) {
                         $wslScriptPath = Convert-WindowsPathToWslPath -WindowsPath $scriptFilePath
                         & wsl.exe -d $InstanceName -u root -- bash $wslScriptPath
                     }
                     else {
                         # Execute via stdin for inline script content
                         $content | & wsl.exe -d $InstanceName -u root -- bash
                     }
                     
                     if ($LASTEXITCODE -ne 0) {
                         throw "Script exited with code $LASTEXITCODE"
                     }
                }
                elseif ($script.Type -eq 'PowerShell' -or $script.Type -eq 1) {
                    if ([string]::IsNullOrWhiteSpace($content)) {
                        Write-Warning "Skipping empty script $($script.Name)"
                        continue
                    }

                    Invoke-Expression $content
                }
             }
             catch {
                 $msg = "Script $($script.Name) failed: $_"
                 if ($script.ContinueOnError) {
                     Write-Warning $msg
                 } else {
                     Write-Error $msg
                     return
                 }
             }
        }
        
        Write-Verbose "Template application completed."
    }
}
