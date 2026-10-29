param(
    [switch]$ResetLegacyDatabase
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$databasePath = Join-Path $projectRoot 'student_activity.db'

Push-Location $projectRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        throw 'Không thể restore dotnet tools.'
    }

    if ($ResetLegacyDatabase -and (Test-Path -LiteralPath $databasePath)) {
        $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
        $backupPath = "$databasePath.legacy-$timestamp.bak"
        $files = @('', '-wal', '-shm', '-journal') | ForEach-Object {
            [pscustomobject]@{
                Source = "${databasePath}$_"
                Backup = "${backupPath}$_"
            }
        }

        # Do not split a live SQLite database from its WAL. Ask the caller to stop API/Visual Studio first.
        foreach ($file in $files) {
            if (Test-Path -LiteralPath $file.Source) {
                if (Test-Path -LiteralPath $file.Backup) {
                    throw "Bản sao lưu đã tồn tại: $($file.Backup)"
                }
                try {
                    $handle = [System.IO.File]::Open(
                        $file.Source,
                        [System.IO.FileMode]::Open,
                        [System.IO.FileAccess]::ReadWrite,
                        [System.IO.FileShare]::None)
                    $handle.Dispose()
                }
                catch {
                    throw "Database đang được sử dụng: $($file.Source). Hãy dừng API/Visual Studio rồi chạy lại."
                }
            }
        }

        $moved = @()
        try {
            foreach ($file in $files) {
                if (Test-Path -LiteralPath $file.Source) {
                    Move-Item -LiteralPath $file.Source -Destination $file.Backup
                    $moved += $file
                }
            }
        }
        catch {
            foreach ($file in $moved) {
                Move-Item -LiteralPath $file.Backup -Destination $file.Source
            }
            throw
        }

        Write-Host "Đã giữ nguyên database cũ và các file SQLite phụ tại: $backupPath"
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
