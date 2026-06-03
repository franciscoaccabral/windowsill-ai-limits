$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsDir = Join-Path $repoRoot "artifacts"
$package = Get-ChildItem -Path $artifactsDir -Filter "WindowSillAiLimits.*.nupkg" -File |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

function Test-LocalizationResources {
    $resourceRoot = Join-Path $repoRoot "src\WindowSillAiLimits\Strings"
    $requiredCultures = @("en-US", "pt-BR")
    $requiredKeys = @(
        "DisplayName",
        "Action.Refresh",
        "Action.Settings",
        "Popup.SourceNote",
        "Pacing.ExpectedSoFar",
        "Pacing.ForecastImpact",
        "Settings.RefreshInterval.Label",
        "Notification.UsageAboveExpected.BodyFormat"
    )

    $baselineKeys = $null
    foreach ($culture in $requiredCultures) {
        $resourcePath = Join-Path $resourceRoot "$culture\Resources.resw"
        if (-not (Test-Path -LiteralPath $resourcePath)) {
            throw "Missing localization resource file: $resourcePath"
        }

        [xml] $resourceXml = Get-Content -LiteralPath $resourcePath -Raw
        $keys = @($resourceXml.root.data | ForEach-Object { $_.name } | Sort-Object -Unique)
        foreach ($requiredKey in $requiredKeys) {
            if (-not ($keys -contains $requiredKey)) {
                throw "$culture localization resource is missing required key: $requiredKey"
            }
        }

        if ($null -eq $baselineKeys) {
            $baselineKeys = $keys
            continue
        }

        $missingComparedToBaseline = Compare-Object -ReferenceObject $baselineKeys -DifferenceObject $keys |
            Select-Object -First 1
        if ($missingComparedToBaseline) {
            throw "$culture localization resource keys do not match the en-US fallback set."
        }
    }

    $legacyResourcePath = Join-Path $resourceRoot "en-US\Misc.resw"
    if (Test-Path -LiteralPath $legacyResourcePath) {
        throw "Legacy Misc.resw is still present; use Resources.resw for WindowSill localization."
    }

    Write-Host "[ok] i18n resources inspected for en-US fallback and pt-BR coverage."
}

if (-not $package) {
    throw "No WindowSillAiLimits .nupkg found in $artifactsDir. Run dotnet build -c Release first."
}

$wsextPath = [IO.Path]::ChangeExtension($package.FullName, ".wsext")
if (-not (Test-Path -LiteralPath $wsextPath) -or
    (Get-FileHash -LiteralPath $package.FullName).Hash -ne (Get-FileHash -LiteralPath $wsextPath).Hash) {
    Copy-Item -LiteralPath $package.FullName -Destination $wsextPath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Test-ExtensionArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,
        [Parameter(Mandatory = $true)]
        [string] $Label
    )

    $zip = [IO.Compression.ZipFile]::OpenRead($Path)

    try {
        $entries = @($zip.Entries | ForEach-Object { $_.FullName })
        $forbiddenEntry = $entries | Where-Object {
            $_ -like "*WindowSillAiLimits.Tests*" -or
            $_ -like "tests/*" -or
            $_ -like "*.pdb" -or
            $_ -like "*.cs"
        } | Select-Object -First 1

        if ($forbiddenEntry) {
            throw "$Label archive contains a test/source/debug entry that should not ship: $forbiddenEntry"
        }

        $forbiddenCredentialEntry = $entries | Where-Object {
            $normalized = $_.Replace("\", "/").ToLowerInvariant()
            $normalized -like "*.env" -or
            $normalized -like "*.user" -or
            $normalized -like "*.suo" -or
            $normalized -like "*auth.json" -or
            $normalized -like "*.credentials.json" -or
            $normalized -like "*credentials.json" -or
            $normalized -like "*secrets.json" -or
            $normalized -like "*.codex/*" -or
            $normalized -like "*.claude/*"
        } | Select-Object -First 1

        if ($forbiddenCredentialEntry) {
            throw "$Label archive contains a credential or local-user config entry that should not ship: $forbiddenCredentialEntry"
        }

        function Assert-PackageEntry {
            param(
                [Parameter(Mandatory = $true)]
                [string] $Pattern,
                [Parameter(Mandatory = $true)]
                [string] $Description
            )

            if (-not ($entries | Where-Object { $_ -like $Pattern })) {
                throw "$Label archive is missing $Description ($Pattern)."
            }
        }

        Assert-PackageEntry "*.nuspec" ".nuspec metadata"
        Assert-PackageEntry "lib/*/WindowSillAiLimits.dll" "main extension DLL"
        Assert-PackageEntry "lib/*/WindowSillAiLimits.pri" "WinUI PRI resource index"
        Assert-PackageEntry "lib/*/WindowSillAiLimits/Views/AiLimitsPopupContent.xbf" "compiled popup XAML resource"
        Assert-PackageEntry "lib/*/WindowSillAiLimits/Assets/openai-mark.svg" "OpenAI provider SVG asset"
        Assert-PackageEntry "lib/*/WindowSillAiLimits/Assets/anthropic-mark.svg" "Anthropic provider SVG asset"
        Assert-PackageEntry "LICENSE.md" "LICENSE.md"
        Assert-PackageEntry "CHANGELOGS.md" "CHANGELOGS.md"
        Assert-PackageEntry "README.md" "README.md"
        Assert-PackageEntry "content/screenshots/compact-bar.png" "compact bar screenshot"
        Assert-PackageEntry "content/screenshots/popup-and-costs.png" "popup and costs screenshot"
        Assert-PackageEntry "content/screenshots/settings.png" "settings screenshot"

        $absoluteEntry = $entries | Where-Object { $_ -match '^[A-Za-z]:\\|^/' } | Select-Object -First 1
        if ($absoluteEntry) {
            throw "$Label archive contains an absolute entry path: $absoluteEntry"
        }

        $nuspec = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
        $reader = [IO.StreamReader]::new($nuspec.Open())
        try {
            $nuspecText = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        [xml] $nuspecXml = $nuspecText
        $namespaceManager = [Xml.XmlNamespaceManager]::new($nuspecXml.NameTable)
        $namespaceManager.AddNamespace("n", "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd")
        $metadata = $nuspecXml.SelectSingleNode("/n:package/n:metadata", $namespaceManager)
        if (-not $metadata) {
            throw "$Label .nuspec is missing metadata."
        }

        function Assert-NuspecText {
            param(
                [Parameter(Mandatory = $true)]
                [string] $Element,
                [Parameter(Mandatory = $true)]
                [string] $Expected
            )

            $node = $metadata.SelectSingleNode("n:$Element", $namespaceManager)
            if (-not $node -or $node.InnerText -ne $Expected) {
                throw "$Label .nuspec is missing required marketplace metadata: $Element=$Expected"
            }
        }

        Assert-NuspecText -Element "id" -Expected "WindowSillAiLimits"
        Assert-NuspecText -Element "title" -Expected "WindowSill AI Limits"
        Assert-NuspecText -Element "authors" -Expected "Francisco Cabral"
        Assert-NuspecText -Element "projectUrl" -Expected "https://github.com/franciscoaccabral/windowsill-ai-limits"
        Assert-NuspecText -Element "readme" -Expected "README.md"
        Assert-NuspecText -Element "icon" -Expected "content/screenshots/compact-bar.png"

        $license = $metadata.SelectSingleNode("n:license", $namespaceManager)
        if (-not $license -or
            $license.InnerText -ne "LICENSE.md" -or
            $license.GetAttribute("type") -ne "file") {
            throw "$Label .nuspec is missing required marketplace metadata: license file LICENSE.md"
        }

        $repository = $metadata.SelectSingleNode("n:repository", $namespaceManager)
        if (-not $repository -or
            $repository.GetAttribute("type") -ne "git" -or
            $repository.GetAttribute("url") -ne "https://github.com/franciscoaccabral/windowsill-ai-limits") {
            throw "$Label .nuspec is missing required marketplace metadata: git repository URL"
        }

        $workspacePathPatterns = @(
            [Regex]::Escape($repoRoot),
            [Regex]::Escape($repoRoot.Replace("\", "/"))
        )

        foreach ($workspacePathPattern in $workspacePathPatterns) {
            if ($nuspecText -match $workspacePathPattern) {
                throw "$Label .nuspec contains a local absolute workspace path."
            }
        }

        $forbiddenTextPatterns = @(
            $repoRoot,
            $repoRoot.Replace("\", "/"),
            "test-access-token",
            "test-refresh-token",
            "secret-token-or-payload",
            "visible-secret",
            "plain-secret",
            "direct-secret",
            "codex-secret",
            "claude-secret",
            "live-secret",
            "other-secret"
        )

        foreach ($entry in $zip.Entries) {
            if ($entry.Length -eq 0 -or $entry.Length -gt 5MB) {
                continue
            }

            $stream = $entry.Open()
            $memory = [IO.MemoryStream]::new()
            try {
                $stream.CopyTo($memory)
                $bytes = $memory.ToArray()
            }
            finally {
                $memory.Dispose()
                $stream.Dispose()
            }

            $entryText = [Text.Encoding]::UTF8.GetString($bytes) + [Text.Encoding]::Unicode.GetString($bytes)
            foreach ($pattern in $forbiddenTextPatterns) {
                if ($entryText.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    throw "$Label archive entry $($entry.FullName) contains forbidden test/local/sensitive text: $pattern"
                }
            }
        }

        Write-Host "[ok] $Label archive inspected: $Path"
    }
    finally {
        $zip.Dispose()
    }
}

Test-ExtensionArchive -Path $package.FullName -Label "NuGet package"
Test-ExtensionArchive -Path $wsextPath -Label "WindowSill extension"
Test-LocalizationResources

$sillSource = Join-Path $repoRoot "src\WindowSillAiLimits\AiLimitsSill.cs"
$sillText = Get-Content -LiteralPath $sillSource -Raw
if ($sillText -match 'new\s+(BitmapIcon|ImageIcon)') {
    throw "CreateIcon appears to use an external icon asset. Add explicit asset validation before installing."
}

Write-Host "[ok] CreateIcon uses a FontIcon; no package icon asset is required."
Write-Host "[ok] WindowSill extension artifact is synchronized with package: $wsextPath"
