function Test-DistroNexusTemplateMetadata {
    [CmdletBinding()]
    [OutputType([PSCustomObject])]
    param(
        [Parameter()]
        [string]$ConfigPath = (Join-Path $script:ProjectRoot 'config\templates.json'),

        [Parameter()]
        [switch]$Strict,

        [Parameter()]
        [string]$ReportPath
    )

    $violations = [System.Collections.Generic.List[object]]::new()

    function Add-LintViolation {
        param(
            [Parameter(Mandatory = $true)]
            [string]$RuleId,
            [Parameter(Mandatory = $true)]
            [ValidateSet('error', 'warning')]
            [string]$Severity,
            [Parameter(Mandatory = $true)]
            [string]$Path,
            [Parameter(Mandatory = $true)]
            [string]$Message,
            [Parameter()]
            [string]$Hint
        )

        $entry = [PSCustomObject]@{
            RuleId = $RuleId
            Severity = $Severity
            Path = $Path
            Message = $Message
            Hint = $Hint
        }

        $null = $violations.Add($entry)
    }

    if (-not (Test-Path $ConfigPath)) {
        Add-LintViolation -RuleId 'metadata.file.exists' -Severity 'error' -Path '$' -Message "Template metadata file not found: $ConfigPath" -Hint 'Provide a valid -ConfigPath pointing to templates.json.'
    }

    $templates = @()
    if ($violations.Count -eq 0) {
        try {
            $rawContent = Get-Content -Path $ConfigPath -Raw -Encoding UTF8
            $parsed = $rawContent | ConvertFrom-Json
            $templates = @($parsed)
            if ($templates.Count -eq 0 -and -not ($parsed -is [System.Array])) {
                Add-LintViolation -RuleId 'metadata.schema.rootArray' -Severity 'error' -Path '$' -Message 'Template metadata root must be a JSON array.' -Hint 'Wrap template objects in a JSON array.'
            }
        }
        catch {
            Add-LintViolation -RuleId 'metadata.json.parse' -Severity 'error' -Path '$' -Message "Failed to parse template metadata JSON: $($_.Exception.Message)" -Hint 'Fix JSON syntax errors in templates.json.'
        }
    }

    $allowedCategories = @('Development', 'Platform', 'CloudNative', 'Database', 'DataAndAI', 'DevOps')
    $requiredTemplateFields = @('Id', 'Name', 'Category', 'Description', 'InstallMode')
    $requiredScriptFields = @('Name', 'ScriptPath', 'Type', 'Phase', 'Order', 'TimeoutSeconds')

    $idIndex = @{}
    $configDirectory = Split-Path -Path $ConfigPath -Parent

    for ($i = 0; $i -lt $templates.Count; $i++) {
        $template = $templates[$i]
        $templatePath = "templates[$i]"

        foreach ($field in $requiredTemplateFields) {
            if (-not ($template.PSObject.Properties.Name -contains $field) -or [string]::IsNullOrWhiteSpace([string]$template.$field)) {
                Add-LintViolation -RuleId 'metadata.template.requiredField' -Severity 'error' -Path "$templatePath.$field" -Message "Missing required template field '$field'." -Hint "Add a non-empty '$field' value."
            }
        }

        if ($template.Id) {
            $templateId = $template.Id.ToString()
            if ($idIndex.ContainsKey($templateId)) {
                Add-LintViolation -RuleId 'metadata.template.duplicateId' -Severity 'error' -Path "$templatePath.Id" -Message "Duplicate template id '$templateId' detected." -Hint "Use unique IDs for each template entry."
            }
            else {
                $idIndex[$templateId] = $templatePath
            }
        }

        if ($template.Category) {
            $category = $template.Category.ToString()
            if ($allowedCategories -notcontains $category) {
                Add-LintViolation -RuleId 'metadata.template.categoryPolicy' -Severity 'error' -Path "$templatePath.Category" -Message "Category '$category' is not in allowed policy set." -Hint ("Use one of: " + ($allowedCategories -join ', '))
            }
        }

        $scripts = @()
        if ($template.PSObject.Properties.Name -contains 'Scripts' -and $template.Scripts) {
            $scripts = @($template.Scripts)
        }

        if ($scripts.Count -eq 0) {
            Add-LintViolation -RuleId 'metadata.template.scriptsRequired' -Severity 'error' -Path "$templatePath.Scripts" -Message 'Each template must define at least one script entry.' -Hint 'Add a Scripts array with valid script metadata.'
            continue
        }

        for ($j = 0; $j -lt $scripts.Count; $j++) {
            $scriptDef = $scripts[$j]
            $scriptPathNode = "$templatePath.Scripts[$j]"

            foreach ($field in $requiredScriptFields) {
                if (-not ($scriptDef.PSObject.Properties.Name -contains $field) -or [string]::IsNullOrWhiteSpace([string]$scriptDef.$field)) {
                    Add-LintViolation -RuleId 'metadata.script.requiredField' -Severity 'error' -Path "$scriptPathNode.$field" -Message "Missing required script field '$field'." -Hint "Provide '$field' in this script definition."
                }
            }

            if ($scriptDef.ScriptPath) {
                $scriptPathValue = $scriptDef.ScriptPath.ToString().Replace('\\', '/')

                if ([System.IO.Path]::IsPathRooted($scriptPathValue)) {
                    Add-LintViolation -RuleId 'metadata.script.pathSafety' -Severity 'error' -Path "$scriptPathNode.ScriptPath" -Message "ScriptPath '$scriptPathValue' must be relative, not absolute." -Hint 'Use relative path under config/templates/.'
                }

                if ($scriptPathValue -match '(^|/)\.\.(/|$)') {
                    Add-LintViolation -RuleId 'metadata.script.pathSafety' -Severity 'error' -Path "$scriptPathNode.ScriptPath" -Message "ScriptPath '$scriptPathValue' contains traversal segments." -Hint 'Remove .. path segments and keep script under templates/.'
                }

                if (-not $scriptPathValue.StartsWith('templates/')) {
                    Add-LintViolation -RuleId 'metadata.script.pathSafety' -Severity 'error' -Path "$scriptPathNode.ScriptPath" -Message "ScriptPath '$scriptPathValue' must start with templates/." -Hint 'Keep script assets under config/templates/.'
                }

                $resolvedScriptPath = Join-Path $configDirectory $scriptPathValue
                if (-not (Test-Path $resolvedScriptPath)) {
                    Add-LintViolation -RuleId 'metadata.script.pathExists' -Severity 'error' -Path "$scriptPathNode.ScriptPath" -Message "Script asset not found: $scriptPathValue" -Hint 'Ensure referenced install script exists in config/templates/.'
                }
            }

            if ($scriptDef.TimeoutSeconds -ne $null) {
                $timeoutValue = 0
                if (-not [int]::TryParse($scriptDef.TimeoutSeconds.ToString(), [ref]$timeoutValue) -or $timeoutValue -le 0) {
                    Add-LintViolation -RuleId 'metadata.script.timeout' -Severity 'error' -Path "$scriptPathNode.TimeoutSeconds" -Message 'TimeoutSeconds must be a positive integer.' -Hint 'Set TimeoutSeconds to a value greater than zero.'
                }
            }

            if ($scriptDef.Type -and $scriptDef.Type.ToString() -ne 'Bash') {
                Add-LintViolation -RuleId 'metadata.script.typePolicy' -Severity 'warning' -Path "$scriptPathNode.Type" -Message "Script type '$($scriptDef.Type)' is uncommon for current templates." -Hint 'Prefer Bash unless a new type policy is intentionally introduced.'
            }
        }
    }

    $errorCount = (@($violations | Where-Object { $_.Severity -eq 'error' })).Count
    $warningCount = (@($violations | Where-Object { $_.Severity -eq 'warning' })).Count

    $result = [PSCustomObject]@{
        SchemaVersion = '1.0'
        Status = if ($errorCount -gt 0) { 'Fail' } else { 'Pass' }
        ConfigPath = $ConfigPath
        StrictMode = [bool]$Strict
        GeneratedAt = (Get-Date).ToString('o')
        Summary = [ordered]@{
            Templates = $templates.Count
            Errors = $errorCount
            Warnings = $warningCount
            Violations = $violations.Count
        }
        SummaryText = "templates=$($templates.Count), errors=$errorCount, warnings=$warningCount, violations=$($violations.Count)"
        Violations = @($violations)
    }

    if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
        $reportDirectory = Split-Path -Path $ReportPath -Parent
        if (-not [string]::IsNullOrWhiteSpace($reportDirectory)) {
            [void](New-Item -Path $reportDirectory -ItemType Directory -Force)
        }

        $result | ConvertTo-Json -Depth 8 | Set-Content -Path $ReportPath -Encoding UTF8
    }

    Write-Information "Template metadata lint summary: $($result.SummaryText)" -InformationAction Continue

    if ($Strict -and $errorCount -gt 0) {
        throw "Template metadata lint failed with $errorCount error(s)."
    }

    return $result
}
