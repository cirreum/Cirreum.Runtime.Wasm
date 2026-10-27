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

# ---------------------------------------------------------------------------------------
# Diagnostics
#
# Warnings and errors are written in MSBuild's canonical format so the Exec task that runs
# this script surfaces them as real build warnings and errors, attributed to the policy
# file that caused them. Every diagnostic carries a stable code so a consuming app can
# suppress one it has accepted (NoWarn) or promote one to a build failure (WarningsAsErrors).
#
#   CSP0001  unsafe keyword on a script directive
#   CSP0002  broad wildcard source
#   CSP0003  scheme-only source
#   CSP0004  object-src widened
#   CSP0005  base-uri widened
#   CSP0006  default-src widened
#   CSP0007  third-party script origin
#   CSP0008  static nonce in a policy file
#   CSP0009  'none' removed from a merged directive
#   CSP0010  unknown directive
#   CSP0011  valueless directive given sources
#   CSP0012  single-value directive replaced rather than unioned
#   CSP0013  unknown quoted keyword
#   CSP0901  unquoted keyword (error)
#   CSP0902  generated config is not valid JSON (error)
#   CSP0903  placeholder survived generation (error)
# ---------------------------------------------------------------------------------------

function Write-CspWarning([string]$code, [string]$origin, [string]$text) {
    Write-Host "$origin : warning ${code}: $text"
}

function Exit-CspError([string]$code, [string]$origin, [string]$text) {
    Write-Host "$origin : error ${code}: $text"
    exit 1
}

# ---------------------------------------------------------------------------------------
# Directive vocabulary
# ---------------------------------------------------------------------------------------

$SourceListDirectives = @(
    "base-uri", "child-src", "connect-src", "default-src", "fenced-frame-src", "font-src",
    "form-action", "frame-ancestors", "frame-src", "img-src", "manifest-src", "media-src",
    "navigate-to", "object-src", "prefetch-src", "script-src", "script-src-attr",
    "script-src-elem", "style-src", "style-src-attr", "style-src-elem", "worker-src"
)

# Token lists that union sensibly but are not source lists (no host/scheme/keyword grammar).
$TokenListDirectives = @("sandbox", "require-trusted-types-for", "trusted-types", "report-uri", "plugin-types")

# Directives that take exactly one value. The highest-precedence policy wins outright;
# unioning two values would produce an invalid directive.
$SingleValueDirectives = @("report-to")

$ValuelessDirectives = @("upgrade-insecure-requests", "block-all-mixed-content")

# Keyword sources must be single-quoted. Written bare, "unsafe-inline" is parsed by the
# browser as a host named unsafe-inline: the policy reads as configured and grants nothing.
$QuotedKeywords = @(
    "self", "none", "unsafe-inline", "unsafe-eval", "unsafe-hashes", "wasm-unsafe-eval",
    "strict-dynamic", "report-sample", "inline-speculation-rules", "unsafe-allow-redirects"
)

$ScriptDirectives = @("default-src", "script-src", "script-src-elem", "script-src-attr", "worker-src")

# Directives where a data:/blob:-style scheme source is a material widening. On img-src,
# font-src, media-src or connect-src the same sources are routine.
$SchemeSensitiveDirectives = $ScriptDirectives + @("object-src", "base-uri", "frame-ancestors", "form-action")

# A bare network scheme is a wildcard over every host on that scheme, whatever the directive.
$NetworkSchemes = @("http", "https", "ws", "wss", "ftp")

function New-SourceSet {
    return New-Object "System.Collections.Generic.HashSet[string]" ([System.StringComparer]::OrdinalIgnoreCase)
}

function Test-IsQuoted([string]$token) {
    return $token.Length -ge 2 -and $token.StartsWith("'") -and $token.EndsWith("'")
}

function Test-IsHashOrNonce([string]$token) {
    return $token -match "^'(nonce|sha256|sha384|sha512)-"
}

function Test-IsSchemeOnly([string]$token) {
    return $token -match "^[a-zA-Z][a-zA-Z0-9+.\-]*:$"
}

function Test-IsBroadWildcard([string]$token) {
    if ($token -eq "*") { return $true }
    # https://* , *:* , https://*:443
    return $token -match "^([a-zA-Z][a-zA-Z0-9+.\-]*://)?\*(:[0-9*]+)?(/.*)?$"
}

# ---------------------------------------------------------------------------------------
# Parsing
# ---------------------------------------------------------------------------------------

function Parse-CspFile($path) {

    if (!(Test-Path $path)) {
        return [ordered]@{}
    }

    $text = Get-Content $path -Raw
    if ($null -eq $text) { $text = "" }
    $text = $text -replace "`r",""

    # Remove whole-line comments BEFORE splitting on ';'. Splitting first would put a
    # comment and the directive beneath it into one chunk, and discarding that chunk on
    # its leading '#' would silently drop the directive with it.
    $text = ($text -split "`n" | Where-Object { $_.Trim() -notmatch '^(#|//)' }) -join "`n"

    $directives = [ordered]@{}

    foreach ($statement in $text -split ";") {

        $line = $statement.Trim()
        if (!$line) { continue }

        $parts = $line -split "\s+"
        $name = $parts[0].ToLowerInvariant()

        # Valueless directives (upgrade-insecure-requests, block-all-mixed-content) carry
        # no source list. Indexing 1..0 on a single-element array is an out-of-range read,
        # which is a terminating error under Set-StrictMode.
        $sources = @()
        if ($parts.Length -gt 1) {
            $sources = $parts[1..($parts.Length-1)]
        }

        if (!$directives.Contains($name)) {
            $directives[$name] = New-SourceSet
        }

        foreach ($s in $sources) {
            if ($s) { $directives[$name].Add($s) | Out-Null }
        }
    }

    return $directives
}

# ---------------------------------------------------------------------------------------
# Validation and risk analysis of one policy file
#
# Syntax problems are reported for every layer, the framework baseline included. Risk
# warnings are reported for app and environment layers only: the baseline's own choices
# ('wasm-unsafe-eval', 'unsafe-inline' on style-src) are the framework's, documented in
# csp.base.policy, and warning on them every build would train apps to ignore the output.
# ---------------------------------------------------------------------------------------

function Test-CspPolicy($directives, [string]$origin, [bool]$isBaseline) {

    foreach ($name in $directives.Keys) {

        $tokens = @($directives[$name])

        $isSourceList = $SourceListDirectives -contains $name
        $isKnown = $isSourceList -or ($TokenListDirectives -contains $name) -or
                   ($SingleValueDirectives -contains $name) -or ($ValuelessDirectives -contains $name)

        if (!$isKnown) {
            Write-CspWarning "CSP0010" $origin "'$name' is not a recognised Content-Security-Policy directive. Browsers ignore directives they do not know, so it will have no effect."
            continue
        }

        if (($ValuelessDirectives -contains $name) -and $tokens.Count -gt 0) {
            Write-CspWarning "CSP0011" $origin "'$name' takes no sources; '$($tokens -join ' ')' will be ignored by the browser."
            continue
        }

        if (!$isSourceList) {
            continue
        }

        $unsafeKeywords = @()
        $wildcards = @()
        $schemes = @()
        $widening = @()
        $scriptHosts = @()
        $nonces = @()

        foreach ($token in $tokens) {

            $lower = $token.ToLowerInvariant()

            # Syntax: a keyword written without quotes is a host name to the browser.
            if (($QuotedKeywords -contains $lower) -or ($lower -match "^(nonce|sha256|sha384|sha512)-")) {
                Exit-CspError "CSP0901" $origin "'$name' contains the keyword $token without quotes. The browser reads it as a host named '$token' and grants nothing; write it as '$token' (single-quoted)."
            }

            if (Test-IsQuoted $token) {
                $keyword = $lower.Trim("'")
                if (!($QuotedKeywords -contains $keyword) -and !(Test-IsHashOrNonce $token)) {
                    Write-CspWarning "CSP0013" $origin "'$name' contains unknown keyword $token, which browsers ignore."
                    continue
                }
            }

            if ($isBaseline) { continue }

            if ($token -match "^'nonce-") {
                $nonces += $token
                continue
            }

            if (Test-IsQuoted $token) {
                $keyword = $lower.Trim("'")
                if (($ScriptDirectives -contains $name) -and ($keyword -in @("unsafe-inline", "unsafe-eval", "unsafe-hashes"))) {
                    $unsafeKeywords += $token
                }
                if ($name -eq "default-src" -and $keyword -ne "self" -and $keyword -ne "none") {
                    $widening += $token
                }
                continue
            }

            if (Test-IsBroadWildcard $token) {
                $wildcards += $token
                continue
            }

            if (Test-IsSchemeOnly $token) {
                $scheme = $lower.TrimEnd(":")
                if (($NetworkSchemes -contains $scheme) -or ($SchemeSensitiveDirectives -contains $name)) {
                    $schemes += $token
                }
                continue
            }

            # Anything left is a host source.
            switch ($name) {
                "object-src"  { $widening += $token }
                "base-uri"    { $widening += $token }
                "default-src" { $widening += $token }
                default {
                    if ($name -in @("script-src", "script-src-elem", "worker-src")) {
                        $scriptHosts += $token
                    }
                }
            }
        }

        if ($unsafeKeywords.Count -gt 0) {
            Write-CspWarning "CSP0001" $origin "'$name' adds $($unsafeKeywords -join ' '). This permits script the policy cannot attribute to a file or hash, which removes most of the cross-site-scripting protection a Content-Security-Policy provides."
        }
        if ($wildcards.Count -gt 0) {
            Write-CspWarning "CSP0002" $origin "'$name' adds $($wildcards -join ' '), which permits any origin. Name the origins the app uses instead."
        }
        if ($schemes.Count -gt 0) {
            Write-CspWarning "CSP0003" $origin "'$name' adds $($schemes -join ' '), which permits every source on that scheme rather than a named origin."
        }
        if ($nonces.Count -gt 0) {
            Write-CspWarning "CSP0008" $origin "'$name' adds $($nonces -join ' '). A nonce in a policy file is the same value on every response, so it identifies nothing; use a hash (CspInlineScriptIds) for inline scripts."
        }
        if ($widening.Count -gt 0) {
            switch ($name) {
                "object-src"  { Write-CspWarning "CSP0004" $origin "'object-src' adds $($widening -join ' '). Plugin content (<object>, <embed>) runs with the page's origin and is blocked by the framework baseline; confirm the app needs it." }
                "base-uri"    { Write-CspWarning "CSP0005" $origin "'base-uri' adds $($widening -join ' '). A permitted <base> origin redirects every relative URL in the document, scripts included." }
                "default-src" { Write-CspWarning "CSP0006" $origin "'default-src' adds $($widening -join ' '). default-src applies to every fetch directive the policy does not list; add the source to the specific directive instead." }
            }
        }
        if ($scriptHosts.Count -gt 0) {
            Write-CspWarning "CSP0007" $origin "'$name' adds $($scriptHosts -join ' '). Script from that origin executes with the page's privileges; prefer a path-qualified source over a whole origin."
        }
    }
}

# ---------------------------------------------------------------------------------------
# Merge
# ---------------------------------------------------------------------------------------

function Merge-Csp($base, $layer, [string]$layerOrigin) {

    foreach ($key in $layer.Keys) {

        if (!$base.Contains($key)) {
            $base[$key] = New-SourceSet
            foreach ($s in $layer[$key]) { $base[$key].Add($s) | Out-Null }
            continue
        }

        if ($SingleValueDirectives -contains $key) {
            $previous = @($base[$key]) -join " "
            $next = @($layer[$key]) -join " "
            if ($previous -and $previous -ne $next) {
                Write-CspWarning "CSP0012" $layerOrigin "'$key' takes a single value; '$next' replaces '$previous' from a lower-precedence policy."
            }
            $base[$key] = New-SourceSet
            foreach ($s in $layer[$key]) { $base[$key].Add($s) | Out-Null }
            continue
        }

        foreach ($s in $layer[$key]) {
            $base[$key].Add($s) | Out-Null
        }
    }

    return $base
}

# A source list containing 'none' alongside other sources: per the specification the
# 'none' is ignored, so emitting it is misleading. Remove it and say which layer widened
# the directive past the 'none' a lower layer had declared.
function Remove-IneffectiveNone($merged, $layers) {

    foreach ($key in @($merged.Keys)) {

        $set = $merged[$key]
        if ($set.Count -le 1 -or !$set.Contains("'none'")) { continue }

        $set.Remove("'none'") | Out-Null

        $declaredNone = @()
        $widened = @()
        foreach ($layer in $layers) {
            if (!$layer.Directives.Contains($key)) { continue }
            $tokens = @($layer.Directives[$key])
            if ($tokens -contains "'none'") { $declaredNone += $layer.Origin }
            if (@($tokens | Where-Object { $_ -ne "'none'" }).Count -gt 0) { $widened += $layer.Origin }
        }

        $origin = if ($widened.Count -gt 0) { $widened[-1] } else { $layers[-1].Origin }
        $from = if ($declaredNone.Count -gt 0) { $declaredNone -join ", " } else { "a lower-precedence policy" }
        Write-CspWarning "CSP0009" $origin "'$key' now lists sources, so the 'none' declared in $from no longer applies and was removed from the generated policy."
    }

    return $merged
}

# Directive names and their sources are both sorted so the generated header is identical
# across builds and the policy diff in a deployment review shows only real changes. The
# sort is ordinal: a culture-aware sort would order punctuation and case differently from
# one build agent to the next.
function Sort-Ordinal([string[]]$values) {
    $copy = @($values)
    [System.Array]::Sort($copy, [System.StringComparer]::Ordinal)
    return $copy
}

function Flatten-Csp($directives) {

    $parts = @()

    foreach ($k in (Sort-Ordinal @($directives.Keys))) {

        # A valueless directive emits its name alone - appending an empty source list
        # would leave a trailing space before the ';'.
        if ($directives[$k].Count -eq 0) {
            $parts += $k
            continue
        }

        $sources = (Sort-Ordinal @($directives[$k])) -join " "
        $parts += "$k $sources"
    }

    return ($parts -join "; ")
}

# ---------------------------------------------------------------------------------------
# Inline script hashing
# ---------------------------------------------------------------------------------------

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

    if (!$directives.Contains("script-src")) {
        $directives["script-src"] = New-SourceSet
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

# ---------------------------------------------------------------------------------------
# Generation
# ---------------------------------------------------------------------------------------

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
    $layers = @()

    $baseDirectives = Parse-CspFile $BasePolicy
    Test-CspPolicy $baseDirectives $BasePolicy $true
    $layers += @{ Origin = $BasePolicy; Directives = $baseDirectives }

    if ($AppPolicy -and (Test-Path $AppPolicy)) {
        Write-Host "Merging app policy: $AppPolicy"
        $app = Parse-CspFile $AppPolicy
        Test-CspPolicy $app $AppPolicy $false
        $layers += @{ Origin = $AppPolicy; Directives = $app }
    } else {
        Write-Host "No app policy at: $AppPolicy"
        Write-Host "  Using the framework baseline alone. The baseline permits same-origin"
        Write-Host "  requests only, so calls to an API on another origin, and any other"
        Write-Host "  third-party resource, must be declared in Properties\csp.policy."
    }

    if ($EnvPolicy -and (Test-Path $EnvPolicy)) {
        Write-Host "Merging env policy: $EnvPolicy"
        $env = Parse-CspFile $EnvPolicy
        Test-CspPolicy $env $EnvPolicy $false
        $layers += @{ Origin = $EnvPolicy; Directives = $env }
    } elseif ($EnvPolicy) {
        Write-Host "Env policy not found at: $EnvPolicy (skipping overlay)"
    }

    $merged = [ordered]@{}
    foreach ($layer in $layers) {
        $merged = Merge-Csp $merged $layer.Directives $layer.Origin
    }

    $merged = Remove-IneffectiveNone $merged $layers

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
    Exit-CspError "CSP0903" $Template ("Placeholder '$Placeholder' is still present after generation. It is supported " +
           "in the value of a Content-Security-Policy entry; anywhere else it cannot be resolved safely.")
}

# Static Web Apps rejects a config it cannot parse at deployment time, long after the
# build that produced it has passed. Parse it here and fail the build instead.
try {
    $null = $templateContent | ConvertFrom-Json
} catch {
    Exit-CspError "CSP0902" $Template "The generated staticwebapp.config.json is not valid JSON: $($_.Exception.Message)"
}

[System.IO.File]::WriteAllText($Output, $templateContent, [System.Text.UTF8Encoding]::new($false))
