[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$container = "proofpath-e2e-$suffix"
$storage = Join-Path ([IO.Path]::GetTempPath()) "proofpath-e2e-$suffix"
$webRoot = Join-Path $PSScriptRoot '..\apps\web'

try {
    New-Item -ItemType Directory -Path $storage | Out-Null
    & docker run --detach --name $container -e POSTGRES_USER=proofpath -e POSTGRES_PASSWORD=synthetic-e2e-only -e POSTGRES_DB=proofpath -p 127.0.0.1::5432 postgres:18-alpine | Out-Null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        & docker exec $container pg_isready -U proofpath -d proofpath *> $null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Seconds 1
    }
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL E2E did not become ready.' }
    $mapping = (& docker port $container 5432/tcp).Trim()
    $port = $mapping.Substring($mapping.LastIndexOf(':') + 1)
    $env:PROOFPATH_E2E_CONNECTION = "Host=127.0.0.1;Port=$port;Database=proofpath;Username=proofpath;Password=synthetic-e2e-only"
    $env:PROOFPATH_E2E_STORAGE = $storage
    Push-Location $webRoot
    try {
        & npx playwright test
        if ($LASTEXITCODE -ne 0) { throw "Playwright failed with exit code $LASTEXITCODE." }
    }
    finally { Pop-Location }
}
finally {
    if ($container -notmatch '^proofpath-e2e-[0-9a-f]{10}$') { throw 'Unexpected E2E container name.' }
    & docker rm --force $container *> $null
    $allowedRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolvedStorage = [IO.Path]::GetFullPath($storage)
    if ($resolvedStorage.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolvedStorage -Leaf) -match '^proofpath-e2e-[0-9a-f]{10}$' -and (Test-Path -LiteralPath $resolvedStorage)) {
        Remove-Item -LiteralPath $resolvedStorage -Recurse -Force
    }
}
