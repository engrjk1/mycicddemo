<#
.SYNOPSIS
  Evaluates the release gates from the Automation Test Implementation Strategy (section 6)
  using the .trx result files produced by every test job.

.DESCRIPTION
  Hard gates (the release cannot proceed if any fail):
    - Unit + integration: 100% of tests pass
    - API / contract:     100% of tests pass   (not in strategy v1.0 - added for this demo)
    - Traceability:       every test is linked to a Jira requirement key (e.g. "LC-101 | ...")
  Soft gate:
    - UI regression: 100% expected. If not met, the deploy waits for project-manager approval.
  Informational:
    - Quarantined (flaky) tests are reported but never block.
#>
param(
    [Parameter(Mandatory)] [string] $ResultsDir,
    [string] $UnitJob = 'unknown',
    [string] $ApiJob = 'unknown',
    [string] $UiJob = 'unknown',
    [string] $RequirementPattern = 'LC-\d+'
)

$ErrorActionPreference = 'Stop'

function Get-Tier([string] $fileName) {
    switch -Regex ($fileName) {
        '^quarantine-' { return 'Quarantined' }
        '^unit'        { return 'Unit + integration' }
        '^api'         { return 'API / contract' }
        '^ui-'         { return 'UI regression' }
        default        { return 'Other' }
    }
}

$runs = @()
$untraced = @()
$failures = @()

foreach ($file in Get-ChildItem -Path $ResultsDir -Filter *.trx -Recurse -ErrorAction SilentlyContinue) {
    [xml] $trx = Get-Content -Raw -Path $file.FullName
    $results = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_ })
    $tier = Get-Tier $file.Name
    $passed = @($results | Where-Object { $_.outcome -eq 'Passed' }).Count
    $failed = @($results | Where-Object { $_.outcome -eq 'Failed' }).Count

    $runs += [pscustomobject]@{
        Tier    = $tier
        Run     = $file.BaseName
        Passed  = $passed
        Failed  = $failed
        Skipped = $results.Count - $passed - $failed
    }
    $untraced += $results | Where-Object { $_.testName -notmatch $RequirementPattern } | ForEach-Object { "$($file.BaseName): $($_.testName)" }
    $failures += $results | Where-Object { $_.outcome -eq 'Failed' } | ForEach-Object { "$($file.BaseName): $($_.testName)" }
}

function Get-Totals([string] $tier) {
    $totals = @{ Passed = 0; Failed = 0 }
    foreach ($r in $runs | Where-Object { $_.Tier -eq $tier }) {
        $totals.Passed += $r.Passed
        $totals.Failed += $r.Failed
    }
    return $totals
}

# A tier passes only if its job succeeded, nothing failed, and at least one test actually ran.
function Test-Tier([string] $tier, [string] $jobResult) {
    $t = Get-Totals $tier
    return ($jobResult -eq 'success' -and $t.Failed -eq 0 -and $t.Passed -gt 0)
}

$unitOk  = Test-Tier 'Unit + integration' $UnitJob
$apiOk   = Test-Tier 'API / contract' $ApiJob
$uiOk    = Test-Tier 'UI regression' $UiJob
$traceOk = $untraced.Count -eq 0
$q = Get-Totals 'Quarantined'
$quarantined = $q.Passed + $q.Failed
$hardOk  = $unitOk -and $apiOk -and $traceOk

function Mark([bool] $ok, [string] $softText = '') {
    if ($ok) { return '✅ Met' }
    if ($softText) { return "⚠️ $softText" }
    return '❌ Not met'
}

$md = @()
$md += '## Release gates'
$md += ''
$md += '| Gate | Target | Result | If not met |'
$md += '|---|---|---|---|'
$md += "| Unit + integration pass rate | 100% | $(Mark $unitOk) | Release cannot proceed |"
$md += "| API / contract pass rate | 100% | $(Mark $apiOk) | Release cannot proceed |"
$md += "| UI regression pass rate | 100% | $(Mark $uiOk 'Not met - PM approval required') | Project manager decides based on severity |"
$md += "| Requirement traceability | Every test linked to a Jira key | $(Mark $traceOk) | Release cannot proceed |"
$md += "| Flaky test handling | Quarantined tests don't block | ℹ️ $quarantined quarantined test result(s) | Informational |"
$md += ''
$md += '### Test results'
$md += ''
$md += '| Tier | Run | Passed | Failed | Skipped |'
$md += '|---|---|---:|---:|---:|'
foreach ($r in $runs | Sort-Object Tier, Run) {
    $md += "| $($r.Tier) | $($r.Run) | $($r.Passed) | $($r.Failed) | $($r.Skipped) |"
}
if ($failures.Count -gt 0) {
    $md += ''
    $md += '### Failed tests'
    $md += ($failures | ForEach-Object { "- $_" })
}
if ($untraced.Count -gt 0) {
    $md += ''
    $md += '### Tests without a Jira requirement key'
    $md += ($untraced | ForEach-Object { "- $_" })
}
if (-not $hardOk) { $verdict = '**Result: BLOCKED** - a hard gate failed.' }
elseif (-not $uiOk) { $verdict = '**Result: NEEDS PM APPROVAL** - UI regression below 100%.' }
else { $verdict = '**Result: READY TO RELEASE**' }
$md += ''
$md += $verdict

$summary = $md -join "`n"
Write-Host $summary
if ($env:GITHUB_STEP_SUMMARY) { $summary | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8 }
if ($env:GITHUB_OUTPUT) {
    "ui_passed=$($uiOk.ToString().ToLower())" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "hard_gates_passed=$($hardOk.ToString().ToLower())" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

if (-not $hardOk) {
    Write-Host '::error::Release blocked: a hard release gate was not met. See the job summary for details.'
    exit 1
}
