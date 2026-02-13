function Apply-DistroNexusTemplate {
    [CmdletBinding()]
    param (
        [Parameter(Mandatory = $true)]
        [string]$InstanceName,

        [Parameter(Mandatory = $true, ParameterSetName = 'ById')]
        [string]$TemplateId,

        [Parameter(Mandatory = $true, ParameterSetName = 'ByObject', ValueFromPipeline = $true)]
        [PSCustomObject]$Template,

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

        foreach ($script in $scripts) {
             $count++
             $percent = ($count / $total) * 100
             Write-Progress -Activity "Applying Template $($Template.Name)" -Status "Executing $($script.Name)" -PercentComplete $percent
             
             try {
                if ($script.Type -eq 'Bash' -or $script.Type -eq 0) { # 0 is Bash enum value
                     $content = $script.Content
                     
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
                    Invoke-Expression $script.Content
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
