param(
    [string]$Template,
    [string]$BasePolicy,
    [string]$AppPolicy,
    [string]$EnvPolicy,
    [string]$Output,
    [string]$Placeholder = "__CSP__",
    [string]$PublishedIndexHtml,
    [string]$InlineScriptIds,
    [string]$GenerateCsp = "true"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Parse-CspFile($path) {

    if (!(Test-Path $path)) {
        return @{}
    }

    $text = Get-Content $path -Raw
    $text = $text -replace "`r",""

    # Remove whole-line comments BEFORE splitting on ';'. Splitting first would put a
    # comment and the directive beneath it into one chunk, and discarding that chunk on
    # its leading '#' would silently drop the directive with it.
    $text = ($text -split "`n" | Where-Object { $_.Trim() -notmatch '^(#|//)' }) -join "`n"

    $directives = @{}

    foreach ($statement in $text -split ";") {

        $line = $statement.Trim()
        if (!$line) { continue }

        if ($line.StartsWith("#") -or $line.StartsWith("//")) {
            continue
        }

        $parts = $line -split "\s+"
        $name = $parts[0]

        # Valueless directives (upgrade-insecure-requests, block-all-mixed-content) carry
        # no source list. Indexing 1..0 on a single-element array is an out-of-range read,
        # which is a terminating error under Set-StrictMode.
        $sources = @()
        if ($parts.Length -gt 1) {
            $sources = $parts[1..($parts.Length-1)]
        }

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

        # A valueless directive emits its name alone - appending an empty source list
        # would leave a trailing space before the ';'.
        if ($directives[$k].Count -eq 0) {
            $parts += $k
            continue
        }

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

# The framework baseline always applies unless the app opts out with
# CirreumGenerateCsp=false, in which case the header is removed from the SWA config
# and the rest of the template (routes, navigation fallback, HSTS) is still emitted.
# App and environment policies layer on top of the baseline when present.
$cspEnabled = $GenerateCsp -ne "false"

$templateContent = Get-Content $Template -Raw

if ($cspEnabled) {

    # Merge order (lowest -> highest precedence):
    #   1. csp.base.policy   (framework defaults - always loaded)
    #   2. csp.policy        (app baseline - if present)
    #   3. csp.{Env}.policy  (deployment overlay - if present)
    $merged = Parse-CspFile $BasePolicy

    if ($AppPolicy -and (Test-Path $AppPolicy)) {
        Write-Host "Merging app policy: $AppPolicy"
        $app = Parse-CspFile $AppPolicy
        $merged = Merge-Csp $merged $app
    } else {
        Write-Host "No app policy at: $AppPolicy"
        Write-Host "  Using the framework baseline alone. The baseline permits same-origin"
        Write-Host "  requests only, so calls to an API on another origin, and any other"
        Write-Host "  third-party resource, must be declared in Properties\csp.policy."
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

    Write-Host "Content-Security-Policy generation is disabled (CirreumGenerateCsp=false)."
    Write-Host "Generating staticwebapp.config.json without a Content-Security-Policy header."

    # Strip the whole Content-Security-Policy entry, leaving valid JSON behind.
    # The key is matched case-insensitively (header names are case-insensitive, and a
    # consuming app's own template may spell it "content-security-policy"), optionally
    # with the -Report-Only suffix, and the value may carry more than the placeholder
    # alone ("__CSP__; report-uri /csp-report").
    # The key is deliberately NOT generalized to "any entry containing the placeholder":
    # that also matches a route or rewrite whose path happens to contain it, and silently
    # deleting an unrelated entry is worse than the placeholder surviving. Anything this
    # does not match is caught by the guard below.
    # Comma handling, in order:
    #   1. entry not first -> eat the leading comma + whitespace
    #   2. entry first     -> eat the trailing comma + whitespace
    #   3. only entry      -> remove it as-is
    $escaped = [regex]::Escape($Placeholder)
    $entry = '"content-security-policy(-report-only)?"\s*:\s*"[^"]*' + $escaped + '[^"]*"'
    $patterns = @(
        ('(?i),\s*' + $entry),
        ('(?i)' + $entry + '\s*,'),
        ('(?i)' + $entry)
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
        Write-Host "Template contains no entry referencing '$Placeholder' - nothing to strip."
    }
}

# The generated file is deployed as-is. A surviving placeholder would ship a header
# the browser cannot parse, which reads as a configured policy while enforcing nothing,
# so fail the build instead of writing it.
if ($templateContent.Contains($Placeholder)) {
    throw ("Placeholder '$Placeholder' is still present after generation. It is supported " +
           "in the value of a Content-Security-Policy entry; anywhere else it cannot be " +
           "resolved safely. Template: $Template")
}

[System.IO.File]::WriteAllText($Output, $templateContent, [System.Text.UTF8Encoding]::new($false))
