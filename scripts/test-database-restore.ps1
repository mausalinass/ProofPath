[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$sourceContainer = "proofpath-restore-source-$suffix"
$targetContainer = "proofpath-restore-target-$suffix"
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "proofpath-restore-$suffix"
$dumpPath = Join-Path $temporaryRoot 'proofpath.dump'
$password = 'synthetic-restore-only'

function Wait-Postgres([string]$container) {
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        & docker exec $container pg_isready -U proofpath -d proofpath *> $null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep -Seconds 1
    }
    throw "PostgreSQL did not become ready in $container."
}

try {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    & docker run --detach --name $sourceContainer -e "POSTGRES_PASSWORD=$password" -e POSTGRES_USER=proofpath -e POSTGRES_DB=proofpath postgres:18-alpine | Out-Null
    & docker run --detach --name $targetContainer -e "POSTGRES_PASSWORD=$password" -e POSTGRES_USER=proofpath -e POSTGRES_DB=proofpath postgres:18-alpine | Out-Null
    Wait-Postgres $sourceContainer
    Wait-Postgres $targetContainer

    & docker exec $sourceContainer psql -v ON_ERROR_STOP=1 -U proofpath -d proofpath -c 'CREATE TABLE restore_evidence(id uuid PRIMARY KEY, label text NOT NULL);' | Out-Null
    & docker exec $sourceContainer psql -v ON_ERROR_STOP=1 -U proofpath -d proofpath -c "INSERT INTO restore_evidence VALUES ('11111111-1111-1111-1111-111111111111', 'synthetic-proofpath-restore');" | Out-Null
    & docker exec $sourceContainer pg_dump -U proofpath -d proofpath --format=custom --file=/tmp/proofpath.dump
    & docker cp "${sourceContainer}:/tmp/proofpath.dump" $dumpPath | Out-Null
    & docker cp $dumpPath "${targetContainer}:/tmp/proofpath.dump" | Out-Null
    & docker exec $targetContainer pg_restore -v --exit-on-error --clean --if-exists -U proofpath -d proofpath /tmp/proofpath.dump *> $null

    $restored = (& docker exec $targetContainer psql -At -U proofpath -d proofpath -c "SELECT label FROM restore_evidence WHERE id='11111111-1111-1111-1111-111111111111';").Trim()
    if ($restored -ne 'synthetic-proofpath-restore') { throw 'Restored data did not match the synthetic source record.' }
    Write-Output 'Database restore test passed: schema and synthetic record restored into an isolated PostgreSQL instance.'
}
finally {
    & docker rm --force $sourceContainer $targetContainer *> $null
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}
