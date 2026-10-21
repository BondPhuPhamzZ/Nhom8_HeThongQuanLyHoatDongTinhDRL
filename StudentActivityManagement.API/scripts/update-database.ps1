param(
    [switch]$ResetLegacyDatabase
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$databasePath = Join-Path $projectRoot 'student_activity.db'

Push-Location $projectRoot
try {
    if ($ResetLegacyDatabase -and (Test-Path -LiteralPath $databasePath)) {
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $backupPath = "$databasePath.legacy-$timestamp.bak"
        Move-Item -LiteralPath $databasePath -Destination $backupPath
        Write-Host "Đã sao lưu database cũ: $backupPath"

        foreach ($suffix in @('-shm', '-wal')) {
            $sidecarPath = "$databasePath$suffix"
            if (Test-Path -LiteralPath $sidecarPath) {
                Remove-Item -LiteralPath $sidecarPath
            }
        }
    }

    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Không thể restore dotnet tools.'
    }

    dotnet ef database update
    if ($LASTEXITCODE -ne 0) {
        throw 'Không thể cập nhật database. Nếu đây là DB Sprint 1 chưa có migration history, chạy lại với -ResetLegacyDatabase.'
    }

    Write-Host 'Database đã được cập nhật theo migration mới nhất.'
}
finally {
    Pop-Location
}
