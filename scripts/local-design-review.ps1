[CmdletBinding()]
param([switch]$Stop)

# Isolated, synthetic-only review. Never connects to staging or production.
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$stateRoot = Join-Path $env:LOCALAPPDATA 'ProofPath\DesignReview'
$manifest = Join-Path $stateRoot 'runtime.json'

function Stop-Review($runtime) {
    foreach ($entry in $runtime.processes) {
        $process = Get-Process -Id $entry.id -ErrorAction SilentlyContinue
        if ($process -and $process.StartTime.ToUniversalTime().Ticks -eq [long]$entry.startedTicks) {
            & taskkill /PID $entry.id /T /F *> $null
        }
    }
    if ($runtime.container -notmatch '^proofpath-design-[0-9a-f]{10}$') { throw 'Unexpected review container name.' }
    & docker rm --force $runtime.container *> $null
}

if ($Stop) {
    if (Test-Path -LiteralPath $manifest) {
        Stop-Review (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)
        Remove-Item -LiteralPath $manifest
        Write-Output 'Local review stopped. Synthetic files and logs remain in LocalAppData/ProofPath/DesignReview.'
    }
    return
}
if (Test-Path -LiteralPath $manifest) { throw 'A review manifest already exists. Run this script with -Stop before starting another review.' }
foreach ($port in @(5001, 5002, 5173)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) { throw "Port $port is occupied. Existing services have not been changed." }
}
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$runtime = [ordered]@{ container = "proofpath-design-$suffix"; processes = @(); url = 'http://localhost:5173'; directory = (Join-Path $stateRoot $suffix) }
New-Item -ItemType Directory -Path $runtime.directory -Force | Out-Null
$oldEnvironment = @{}
$settings = @{
    ASPNETCORE_ENVIRONMENT = 'Testing'
    OpenAI__ApiKey = ''
    GitHub__PrivateKeyPem = ''
    GitHub__PrivateKeyPath = ''
    GitHub__AppSlug = ''
    GitHub__AppId = ''
    Security__AuthRequestsPerMinute = '10000'
}
function Save-State { $runtime | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest }
function Start-ServiceProcess($name, $exe, $arguments, $directory, $health) {
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $directory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime.directory "$name.log") -RedirectStandardError (Join-Path $runtime.directory "$name-error.log")
    $runtime.processes += @{ id = $process.Id; startedTicks = $process.StartTime.ToUniversalTime().Ticks; name = $name }
    Save-State
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        $process.Refresh()
        if ($process.HasExited) { throw "$name exited. Review its local logs." }
        try { if ((Invoke-WebRequest -Uri $health -TimeoutSec 2).StatusCode -eq 200) { return } } catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "$name did not become ready. Review its local logs."
}
try {
    Save-State
    $password = [Guid]::NewGuid().ToString('N')
    & docker run --detach --name $runtime.container -e POSTGRES_USER=proofpath -e "POSTGRES_PASSWORD=$password" -e POSTGRES_DB=proofpath -p 127.0.0.1::5432 postgres:18-alpine | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not start the isolated local database.' }
    Save-State
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        & docker exec $runtime.container pg_isready -U proofpath -d proofpath *> $null
        if ($LASTEXITCODE -eq 0) { break }
        Start-Sleep -Milliseconds 500
    }
    if ($LASTEXITCODE -ne 0) { throw 'Local database did not become ready.' }
    $mapping = (& docker port $runtime.container 5432/tcp).Trim()
    $dbPort = $mapping.Substring($mapping.LastIndexOf(':') + 1)
    $connection = "Host=127.0.0.1;Port=$dbPort;Database=proofpath;Username=proofpath;Password=$password"
    $settings.ConnectionStrings__DefaultConnection = $connection
    $settings.PROOFPATH_E2E_CONNECTION = $connection
    $settings.PROOFPATH_E2E_STORAGE = Join-Path $runtime.directory 'private-storage'
    $settings.Storage__PrivateRoot = $settings.PROOFPATH_E2E_STORAGE
    foreach ($key in $settings.Keys) {
        $oldEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process')
    }
    Start-ServiceProcess 'fixture-worker' 'dotnet' 'run --project apps/backend/tests/ProofPath.E2EHost --no-launch-profile -- --urls http://localhost:5002' $repoRoot 'http://localhost:5002/health'
    Start-ServiceProcess 'api' 'dotnet' 'run --project apps/backend/src/ProofPath.Api --no-launch-profile -- --urls http://localhost:5001' $repoRoot 'http://localhost:5001/health'
    Start-ServiceProcess 'web' 'node' 'node_modules/vite/bin/vite.js --host localhost --port 5173 --strictPort' (Join-Path $repoRoot 'apps/web') 'http://localhost:5173'
    Write-Output 'ProofPath review is ready at http://localhost:5173. Use synthetic data only; extraction uses the local fixture provider. GitHub credentials are intentionally absent.'
}
catch {
    Stop-Review $runtime
    if (Test-Path -LiteralPath $manifest) { Remove-Item -LiteralPath $manifest }
    throw
}
finally {
    foreach ($key in $oldEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key, $oldEnvironment[$key], 'Process') }
}
