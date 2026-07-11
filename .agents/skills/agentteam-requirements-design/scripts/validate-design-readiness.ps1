[CmdletBinding(DefaultParameterSetName = 'Path')]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Path')][string]$RequirementsPath,
    [Parameter(Mandatory = $true, ParameterSetName = 'Path')][string]$DesignPath,
    [Parameter(Mandatory = $true, ParameterSetName = 'SelfTest')][switch]$SelfTest
)
$ErrorActionPreference = 'Stop'
function Test-DesignReadiness { param([string]$Requirements, [string]$Design)
    $errors = [System.Collections.Generic.List[string]]::new()
    foreach ($heading in @('## Scope', '## Functional Requirements', '## Acceptance Criteria', '## Source Evidence')) { if (-not $Requirements.Contains($heading)) { $errors.Add("Requirements missing: $heading") } }
    foreach ($heading in @('## Scope and Requirement Traceability', '## Architecture and Ownership', '## Contracts and Behavior', '## Data and Execution Semantics', '## Verification Strategy', '## Open Items')) { if (-not $Design.Contains($heading)) { $errors.Add("Design missing: $heading") } }
    if ([regex]::Matches($Requirements, '(?m)^### (?:[A-Z]+-)?FR-\d+').Count -eq 0) { $errors.Add('Requirements contain no numbered functional requirement.') }
    if ($Design -notmatch '(?m)^\|\s*Requirement\s*\|\s*Design section\s*\|\s*Test or verification\s*\|') { $errors.Add('Design lacks the requirement traceability table.') }
    if ($Requirements -match '\{\{[^}]+\}\}' -or $Design -match '\{\{[^}]+\}\}') { $errors.Add('Unfilled {{PLACEHOLDER}} remains.') }
    return $errors
}
if ($SelfTest) {
    $validRequirements = "## Scope`nvalue`n## Functional Requirements`n### FR-001 behavior`n## Acceptance Criteria`nvalue`n## Source Evidence`nvalue"
    $validDesign = "## Scope and Requirement Traceability`n| Requirement | Design section | Test or verification |`n| --- | --- | --- |`n| FR-001 | Contracts | unit test |`n## Architecture and Ownership`nvalue`n## Contracts and Behavior`nvalue`n## Data and Execution Semantics`nvalue`n## Verification Strategy`nvalue`n## Open Items`nvalue"
    if (@(Test-DesignReadiness $validRequirements $validDesign).Count -ne 0 -or @(Test-DesignReadiness '# invalid' '# invalid').Count -eq 0) { throw 'Self-test failed.' }
    'PASS: validate-design-readiness self-test'; exit 0
}
$errors = @(Test-DesignReadiness (Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $RequirementsPath)) (Get-Content -Raw -LiteralPath (Resolve-Path -LiteralPath $DesignPath)))
if ($errors.Count) { $errors | ForEach-Object { Write-Error $_ }; exit 1 }
'PASS: requirements and technical design are structurally ready for slice planning.'
