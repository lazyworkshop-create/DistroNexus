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
                $content = Get-ScriptContent -Script $script

                if ($Template.PSObject.Properties.Name -contains 'Variables' -and $Template.Variables) {
                    $content = Apply-TemplateVariables -Content $content -VariableMap $Template.Variables
                }

                if ($Variables) {
                    $content = Apply-TemplateVariables -Content $content -VariableMap $Variables
                }

                if ($script.Type -eq 'Bash' -or $script.Type -eq 0) { # 0 is Bash enum value
                     if ([string]::IsNullOrWhiteSpace($content)) {
                         Write-Warning "Skipping empty script $($script.Name)"
                         continue
                     }

                     # Execute via stdin to avoid quoting hell
                     # Using invocation operator & to call wsl
                     $content | & wsl.exe -d $InstanceName -- bash
                     
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
