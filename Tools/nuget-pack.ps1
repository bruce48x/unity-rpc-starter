$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\ULinkRPC.Runtime\ULinkRPC.Runtime.csproj"
$artifacts = Join-Path $root "artifacts"

if (!(Test-Path $artifacts)) {
    New-Item -ItemType Directory -Path $artifacts | Out-Null
}

dotnet pack $project -c Release -o $artifacts
