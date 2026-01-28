$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$apiKey = $env:NUGET_API_KEY

if ([string]::IsNullOrWhiteSpace($apiKey)) {
    throw "NUGET_API_KEY is not set."
}

if (!(Test-Path $artifacts)) {
    throw "Artifacts folder not found. Run Tools/nuget-pack.ps1 first."
}

$packages = Get-ChildItem -Path $artifacts -Filter "ULinkRPC.Runtime*.nupkg"
if ($packages.Count -eq 0) {
    throw "No ULinkRPC.Runtime package found in artifacts."
}

foreach ($pkg in $packages) {
    dotnet nuget push $pkg.FullName --api-key $apiKey --source "https://api.nuget.org/v3/index.json" --skip-duplicate
}
