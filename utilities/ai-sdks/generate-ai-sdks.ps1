<#
.SYNOPSIS
    Generates TypeScript and Python clients for the AI API from a tenant's OpenAPI document.

.DESCRIPTION
    Downloads the OpenAPI document of a site that has the OpenApi and AI Chat WebAPI features on, and runs Kiota
    (pinned in dotnet-tools.json) to generate a client per language for the /api/ai paths. Nothing is published:
    the clients are written under the output folder for you to package and version as you choose.

.PARAMETER SiteUrl
    The tenant's base address, such as https://localhost:5001 or https://example.com/tenant.

.PARAMETER OpenApiFile
    A local OpenAPI document to use instead of downloading one.

.PARAMETER ApiKey
    An AI API server key, sent as X-Api-Key, when the document needs authentication. Defaults to $env:CRESTAPPS_AI_API_KEY.

.PARAMETER Languages
    The languages to generate: TypeScript, Python, CSharp, Java, Go or PHP. Defaults to TypeScript and Python.

.PARAMETER Output
    Where to write the clients. Defaults to artifacts/ai-sdks under the repository root.

.EXAMPLE
    ./utilities/ai-sdks/generate-ai-sdks.ps1 -SiteUrl https://localhost:5001
#>
[CmdletBinding()]
param(
    [string] $SiteUrl,
    [string] $OpenApiFile,
    [string] $ApiKey = $env:CRESTAPPS_AI_API_KEY,
    [string[]] $Languages = @('TypeScript', 'Python'),
    [string] $Output
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '../..')

if (-not $Output) {
    $Output = Join-Path $repositoryRoot 'artifacts/ai-sdks'
}

if (-not $OpenApiFile) {
    if (-not $SiteUrl) {
        throw 'Give -SiteUrl to download the OpenAPI document, or -OpenApiFile to use a local one.'
    }

    New-Item -ItemType Directory -Force $Output | Out-Null
    $OpenApiFile = Join-Path $Output 'openapi.json'
    $headers = @{}

    if ($ApiKey) {
        $headers['X-Api-Key'] = $ApiKey
    }

    $documentUrl = "$($SiteUrl.TrimEnd('/'))/swagger/v1/swagger.json"
    Write-Host "Downloading $documentUrl"
    Invoke-WebRequest -Uri $documentUrl -Headers $headers -OutFile $OpenApiFile
}

Push-Location $repositoryRoot

try {
    dotnet tool restore | Out-Null

    $names = @{
        TypeScript = @{ Language = 'typescript'; Namespace = 'crestapps.ai' }
        Python     = @{ Language = 'python'; Namespace = 'crestapps_ai' }
        CSharp     = @{ Language = 'csharp'; Namespace = 'CrestApps.AI.Client' }
        Java       = @{ Language = 'java'; Namespace = 'com.crestapps.ai' }
        Go         = @{ Language = 'go'; Namespace = 'crestapps/ai' }
        PHP        = @{ Language = 'php'; Namespace = 'CrestApps\AI' }
    }

    foreach ($language in $Languages) {
        $target = $names[$language]

        if (-not $target) {
            throw "Kiota does not generate '$language' here. Use TypeScript, Python, CSharp, Java, Go or PHP."
        }

        $folder = Join-Path $Output $language.ToLowerInvariant()
        Write-Host "Generating the $language client in $folder"

        dotnet kiota generate `
            --openapi $OpenApiFile `
            --language $target.Language `
            --class-name CrestAppsAIClient `
            --namespace-name $target.Namespace `
            --include-path '/api/ai/**' `
            --output $folder `
            --clean-output `
            --exclude-backward-compatible

        if ($LASTEXITCODE -ne 0) {
            throw "Kiota could not generate the $language client."
        }
    }
}
finally {
    Pop-Location
}

Write-Host "Done. The clients are in $Output."
