[CmdletBinding(DefaultParameterSetName = 'Path')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Path')][string]$Path,
    [Parameter(Mandatory = $true, ParameterSetName = 'SelfTest')][switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
function Test-SlicePlanContent { param([string]$Content)
    $errors = [System.Collections.Generic.List[string]]::new()
    foreach ($heading in @('# Implementation Slice Plan:', '## Sources', '## Dependency Order')) { if (-not $Content.Contains($heading)) { $errors.Add("Missing required heading: $heading") } }
    $sliceMatches = [regex]::Matches($Content, '(?m)^## Slice (?<id>S\d{2,}):[^\r\n]+')
    if ($sliceMatches.Count -eq 0) { $errors.Add('No slice headings found. Expected: ## Slice S01: <outcome>'); return $errors }
    @($sliceMatches | ForEach-Object { $_.Groups['id'].Value } | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name) | ForEach-Object { $errors.Add("Duplicate slice id: $_") }
    if ($Content -match '\{\{[^}]+\}\}') { $errors.Add('Unfilled {{PLACEHOLDER}} remains in the slice plan.') }
    if ($Content -match '(?im)^\s*TODO\s*:') { $errors.Add('A TODO remains in the slice plan; resolve it or block planning before delegation.') }
    $sections = @('Status','Objective','Sources','Dependencies','Allowed Paths','Excluded Paths','Contract and Documentation','Implementation Scope','Test Scope','Acceptance Criteria','Verification Commands','Commit Boundary','Out of Scope')
    for ($i = 0; $i -lt $sliceMatches.Count; $i++) { $start = $sliceMatches[$i].Index; $length = if ($i + 1 -lt $sliceMatches.Count) { $sliceMatches[$i + 1].Index - $start } else { $Content.Length - $start }; $block = $Content.Substring($start, $length); $id = $sliceMatches[$i].Groups['id'].Value
        foreach ($section in $sections) {
            $m = [regex]::Match($block, "(?ms)^### $([regex]::Escape($section))\s*\r?\n(?<body>.*?)(?=^### |\z)")
            if (-not $m.Success) { $errors.Add("Slice $id is missing section: ### $section") }
            elseif ([string]::IsNullOrWhiteSpace($m.Groups['body'].Value)) { $errors.Add("Slice $id has an empty section: ### $section") }
            elseif ($section -eq 'Status' -and $m.Groups['body'].Value.Trim() -notmatch '^(Planned|In Progress|Blocked|Accepted|Committed|Completed)\.?$') { $errors.Add("Slice $id has an invalid status.") }
            elseif ($section -eq 'Acceptance Criteria' -and $m.Groups['body'].Value -notmatch '(?m)^\s*-\s+\S+') { $errors.Add("Slice $id acceptance criteria need at least one bullet.") }
        }
    }; return $errors
}
if ($SelfTest) { $sections = @('Status','Objective','Sources','Dependencies','Allowed Paths','Excluded Paths','Contract and Documentation','Implementation Scope','Test Scope','Acceptance Criteria','Verification Commands','Commit Boundary','Out of Scope') | ForEach-Object { if ($_ -eq 'Status') { "### $_`n`nPlanned" } elseif ($_ -eq 'Acceptance Criteria') { "### $_`n`n- Observable result" } else { "### $_`n`nValue" } }; $valid = "# Implementation Slice Plan: Self Test`n`n## Sources`n`nValue`n`n## Dependency Order`n`nS01`n`n## Slice S01: Outcome`n`n$($sections -join "`n`n")"; if (@(Test-SlicePlanContent $valid).Count -ne 0 -or @(Test-SlicePlanContent '# Invalid').Count -eq 0) { throw 'Self-test failed.' }; 'PASS: validate-slice-plan self-test'; exit 0 }
$errors = @(Test-SlicePlanContent (Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $Path)))
if ($errors.Count) {
    foreach ($validationError in $errors) { [Console]::Error.WriteLine($validationError) }
    exit 1
}
"PASS: $Path"
