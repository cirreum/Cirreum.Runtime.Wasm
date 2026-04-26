param(
    [string]$Template,
    [string]$BasePolicy,
    [string]$AppPolicy,
    [string]$EnvPolicy,
    [string]$Output,
    [string]$Placeholder = "__CSP__",
    [string]$PublishedIndexHtml,
    [string]$InlineScriptIds
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

function Get-Sha256Token($content) {

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha.ComputeHash($bytes)
        $b64 = [Convert]::ToBase64String($hashBytes)
        return "'sha256-$b64'"
    } finally {
        $sha.Dispose()
    }
}

function Add-ScriptSrcToken($directives, $token) {

    if (!$directives.ContainsKey("script-src")) {
        $directives["script-src"] = New-Object System.Collections.Generic.HashSet[string]
    }
    $directives["script-src"].Add($token) | Out-Null
}

function Get-InlineScriptHashes($html, $extraIds) {

    $tokens = @()

    # Always hash the importmap (Blazor populates this at publish time)
    $importMapPattern = '(?is)<script\b[^>]*\btype\s*=\s*"importmap"[^>]*>(.*?)</script>'
    foreach ($m in [regex]::Matches($html, $importMapPattern)) {
        $body = $m.Groups[1].Value
        $tokens += Get-Sha256Token $body
        Write-Host "  + importmap hash"
    }

    # Hash any opt-in inline <script id="..."> elements named in $extraIds
    if ($extraIds) {
        $ids = $extraIds -split "[,;\s]+" | Where-Object { $_ }
        foreach ($id in $ids) {
            $escaped = [regex]::Escape($id)
            $idPattern = "(?is)<script\b[^>]*\bid\s*=\s*`"$escaped`"[^>]*>(.*?)</script>"
            foreach ($m in [regex]::Matches($html, $idPattern)) {
                $body = $m.Groups[1].Value
                $tokens += Get-Sha256Token $body
                Write-Host "  + inline script hash for id='$id'"
            }
        }
    }

    return $tokens
}

# Track whether the consuming app opted into CSP. The app opts in by providing
# at least one of:
#   - Properties\csp.policy           (the app's primary baseline)
#   - Properties\csp.{Env}.policy     (deployment-environment overlay; only
#                                      passed in when EnvironmentName /
#                                      CspEnvironmentName resolved to a value)
# If neither exists, the CSP header is stripped from the SWA config but the
# rest of the template (routes, navigation fallback, HSTS, etc.) is emitted.
$hasAppPolicy = ($AppPolicy -and (Test-Path $AppPolicy)) -or `
                ($EnvPolicy -and (Test-Path $EnvPolicy))

$templateContent = Get-Content $Template -Raw

if ($hasAppPolicy) {

    # Merge order (lowest -> highest precedence):
    #   1. csp.base.policy   (framework defaults — always loaded)
    #   2. csp.policy        (app baseline — if present)
    #   3. csp.{Env}.policy  (deployment overlay — if present)
    $merged = Parse-CspFile $BasePolicy

    if ($AppPolicy -and (Test-Path $AppPolicy)) {
        Write-Host "Merging app policy: $AppPolicy"
        $app = Parse-CspFile $AppPolicy
        $merged = Merge-Csp $merged $app
    }

    if ($EnvPolicy -and (Test-Path $EnvPolicy)) {
        Write-Host "Merging env policy: $EnvPolicy"
        $env = Parse-CspFile $EnvPolicy
        $merged = Merge-Csp $merged $env
    } elseif ($EnvPolicy) {
        Write-Host "Env policy not found at: $EnvPolicy (skipping overlay)"
    }

    # Hash inline scripts from the published index.html (importmap + opt-in ids)
    if ($PublishedIndexHtml -and (Test-Path $PublishedIndexHtml)) {
        Write-Host "Hashing inline scripts from: $PublishedIndexHtml"
        $html = Get-Content $PublishedIndexHtml -Raw
        $hashes = Get-InlineScriptHashes $html $InlineScriptIds
        foreach ($t in $hashes) {
            Add-ScriptSrcToken $merged $t
        }
    } elseif ($PublishedIndexHtml) {
        Write-Host "Published index.html not found at: $PublishedIndexHtml (skipping inline-script hashing)"
    }

    $csp = Flatten-Csp $merged
    $templateContent = $templateContent.Replace($Placeholder, $csp)

    Write-Host "CSP generated:"
    Write-Host $csp

} else {

    Write-Host "No app-specific CSP policy files found."
    Write-Host "  Looked for app policy: $AppPolicy"
    if ($EnvPolicy) { Write-Host "  Looked for env policy: $EnvPolicy" }
    Write-Host "Generating staticwebapp.config.json without a Content-Security-Policy header."

    # Strip the Content-Security-Policy line entirely. Try in order:
    #   1. CSP not first  -> eat the leading comma + whitespace
    #   2. CSP first      -> eat the trailing comma + whitespace
    #   3. CSP only entry -> remove the line as-is
    $patterns = @(
        ',\s*"Content-Security-Policy"\s*:\s*"' + [regex]::Escape($Placeholder) + '"',
        '"Content-Security-Policy"\s*:\s*"' + [regex]::Escape($Placeholder) + '"\s*,',
        '"Content-Security-Policy"\s*:\s*"' + [regex]::Escape($Placeholder) + '"'
    )

    $stripped = $false
    foreach ($p in $patterns) {
        if ([regex]::IsMatch($templateContent, $p)) {
            $templateContent = [regex]::Replace($templateContent, $p, "")
            $stripped = $true
            break
        }
    }

    if (!$stripped) {
        Write-Host "Warning: template did not contain a Content-Security-Policy entry referencing '$Placeholder' — nothing to strip."
    }
}

[System.IO.File]::WriteAllText($Output, $templateContent, [System.Text.UTF8Encoding]::new($false))
