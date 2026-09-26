<#
.SYNOPSIS
  Posts every test result to Jira/Xray (Xray Cloud) automatically - no manual copying from build logs.

.DESCRIPTION
  Creates one Test Execution per result file (unit, api, ui-chromium, ...). Each test is matched to an
  Xray "Generic" test by its full name (created on first run) and linked to the Jira requirement keys
  found in its name, e.g. "LC-105 | Double-booking a provider is rejected with 409" -> LC-105.

  Skips quietly when credentials are not configured, so the pipeline works before Xray is set up.
  Needs: XRAY_CLIENT_ID, XRAY_CLIENT_SECRET (secrets) and XRAY_PROJECT_KEY (variable).
#>
param(
    [Parameter(Mandatory)] [string] $ResultsDir,
    [string] $RequirementPattern = 'LC-\d+',
    [string] $XrayBaseUrl = 'https://xray.cloud.getxray.app/api/v2'
)

$ErrorActionPreference = 'Stop'

function Write-Summary([string] $text) {
    Write-Host $text
    if ($env:GITHUB_STEP_SUMMARY) { $text | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8 }
}

$clientId = $env:XRAY_CLIENT_ID
$clientSecret = $env:XRAY_CLIENT_SECRET
$projectKey = $env:XRAY_PROJECT_KEY
if (-not $clientId -or -not $clientSecret -or -not $projectKey) {
    Write-Summary "`n## Jira/Xray sync`nSkipped: add the XRAY_CLIENT_ID and XRAY_CLIENT_SECRET secrets and the XRAY_PROJECT_KEY variable to turn it on."
    exit 0
}

$token = Invoke-RestMethod -Method Post -Uri "$XrayBaseUrl/authenticate" -ContentType 'application/json' `
    -Body (@{ client_id = $clientId; client_secret = $clientSecret } | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $token" }

$sha = "$env:GITHUB_SHA"
$shortSha = $sha.Substring(0, [Math]::Min(7, $sha.Length))
$runUrl = "$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
$lines = @("`n## Jira/Xray sync", '')

foreach ($file in Get-ChildItem -Path $ResultsDir -Filter *.trx -Recurse) {
    [xml] $trx = Get-Content -Raw -Path $file.FullName
    $results = @($trx.TestRun.Results.UnitTestResult | Where-Object { $_ })
    if ($results.Count -eq 0) { continue }

    $tests = foreach ($r in $results) {
        $name = [string] $r.testName
        $keys = @([regex]::Matches($name, $RequirementPattern) | ForEach-Object { $_.Value } | Select-Object -Unique)
        $status = switch ($r.outcome) { 'Passed' { 'PASSED' } 'Failed' { 'FAILED' } default { 'TODO' } }
        $summaryText = ($name -replace "^\s*(($RequirementPattern)[\s,]*)+\|\s*", '')
        if ($summaryText.Length -gt 250) { $summaryText = $summaryText.Substring(0, 250) }
        @{
            testInfo = @{
                projectKey      = $projectKey
                summary         = $summaryText
                type            = 'Generic'
                definition      = $name
                requirementKeys = $keys
            }
            status   = $status
            comment  = if ($r.outcome -eq 'Failed') { [string] $r.Output.ErrorInfo.Message } else { '' }
        }
    }

    $info = @{
        project     = $projectKey
        summary     = "MyCare CI #$env:GITHUB_RUN_NUMBER - $($file.BaseName) - $shortSha"
        description = "Automated results for commit $sha. Workflow run: $runUrl"
    }
    if ($file.BaseName -match '^ui-(\w+)$') { $info.testEnvironments = @($Matches[1]) }

    $body = @{ info = $info; tests = @($tests) } | ConvertTo-Json -Depth 8
    $response = Invoke-RestMethod -Method Post -Uri "$XrayBaseUrl/import/execution" -Headers $headers -ContentType 'application/json' -Body $body
    $lines += "- $($file.BaseName): $($results.Count) results -> Test Execution **$($response.key)**"
}

Write-Summary ($lines -join "`n")
