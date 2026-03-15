param(
    [string]$Template,
    [string]$BasePolicy,
    [string]$EnvPolicy,
    [string]$Output,
    [string]$Placeholder = "__CSP__"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Parse-CspFile($path) {

    if (!(Test-Path $path)) {
        return @{}
    }

    $text = Get-Content $path -Raw
    $text = $text -replace "`r",""

    $directives = @{}

    foreach ($statement in $text -split ";") {

        $line = $statement.Trim()
        if (!$line) { continue }

        if ($line.StartsWith("#") -or $line.StartsWith("//")) {
            continue
        }

        $parts = $line -split "\s+"
        $name = $parts[0]
        $sources = $parts[1..($parts.Length-1)]

        if (!$directives.ContainsKey($name)) {
            $directives[$name] = New-Object System.Collections.Generic.HashSet[string]
        }

        foreach ($s in $sources) {
            if ($s) { $directives[$name].Add($s) | Out-Null }
        }
    }

    return $directives
}

function Merge-Csp($base, $env) {

    foreach ($key in $env.Keys) {

        if (!$base.ContainsKey($key)) {
            $base[$key] = $env[$key]
            continue
        }

        foreach ($s in $env[$key]) {
            $base[$key].Add($s) | Out-Null
        }
    }

    return $base
}

function Flatten-Csp($directives) {

    $parts = @()

    foreach ($k in $directives.Keys | Sort-Object) {

        $sources = $directives[$k] -join " "
        $parts += "$k $sources"
    }

    return ($parts -join "; ")
}

# Always start with base
$merged = Parse-CspFile $BasePolicy

# Derive Production policy path from EnvPolicy location
$prodPolicy = [System.IO.Path]::Combine(
    [System.IO.Path]::GetDirectoryName($EnvPolicy),
    "csp.Production.policy"
)

$isProduction = [System.IO.Path]::GetFullPath($EnvPolicy) -eq [System.IO.Path]::GetFullPath($prodPolicy)

# Always merge Production if it exists
if (Test-Path $prodPolicy) {
    Write-Host "Merging Production policy: $prodPolicy"
    $prod = Parse-CspFile $prodPolicy
    $merged = Merge-Csp $merged $prod
}

# Merge env-specific policy on top if not Production
if (!$isProduction -and (Test-Path $EnvPolicy)) {
    Write-Host "Merging environment policy: $EnvPolicy"
    $env = Parse-CspFile $EnvPolicy
    $merged = Merge-Csp $merged $env
} elseif (!$isProduction -and !(Test-Path $EnvPolicy)) {
    Write-Host "No environment policy found at: $EnvPolicy (skipping)"
}

$csp = Flatten-Csp $merged

$templateContent = Get-Content $Template -Raw
$templateContent = $templateContent.Replace($Placeholder, $csp)

[System.IO.File]::WriteAllText($Output, $templateContent, [System.Text.UTF8Encoding]::new($false))

Write-Host "CSP generated:"
Write-Host $csp