[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'StudentActivityManagement.API.csproj'
$secretKey = 'JwtSettings:Secret'

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Không tìm thấy project tại: $projectPath"
}

if (-not [string]::IsNullOrWhiteSpace($env:JwtSettings__Secret)) {
    if ($env:JwtSettings__Secret.Length -lt 32) {
        throw "Biến môi trường JwtSettings__Secret đang tồn tại nhưng ngắn hơn 32 ký tự và sẽ ghi đè user-secrets. Hãy chạy: Remove-Item Env:JwtSettings__Secret"
    }

    Write-Host 'JwtSettings__Secret đã được cấu hình bằng biến môi trường; không thay đổi user-secrets.'
    exit 0
}

$secretLines = @(& dotnet user-secrets list --project $projectPath 2>&1)
if ($LASTEXITCODE -ne 0) {
    throw "Không thể đọc user-secrets: $($secretLines -join [Environment]::NewLine)"
}

$secretLine = $secretLines |
    Where-Object { $_ -match '^\s*JwtSettings:Secret\s*=' } |
    Select-Object -First 1

$existingSecret = if ($secretLine) { ($secretLine -split '=', 2)[1].Trim() } else { $null }
if (-not $Force -and -not [string]::IsNullOrWhiteSpace($existingSecret) -and $existingSecret.Length -ge 32) {
    Write-Host 'JwtSettings:Secret đã hợp lệ trong user-secrets; không tạo khóa mới.'
    exit 0
}

$secretBytes = New-Object byte[] 48
$random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $random.GetBytes($secretBytes)
}
finally {
    $random.Dispose()
}

$generatedSecret = [Convert]::ToBase64String($secretBytes)
try {
    & dotnet user-secrets set $secretKey $generatedSecret --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'dotnet user-secrets set không thành công.'
    }
}
finally {
    $generatedSecret = $null
    [Array]::Clear($secretBytes, 0, $secretBytes.Length)
}

$verificationLines = @(& dotnet user-secrets list --project $projectPath 2>&1)
$verificationLine = $verificationLines |
    Where-Object { $_ -match '^\s*JwtSettings:Secret\s*=' } |
    Select-Object -First 1
$verificationValue = if ($verificationLine) { ($verificationLine -split '=', 2)[1].Trim() } else { $null }

if ([string]::IsNullOrWhiteSpace($verificationValue) -or $verificationValue.Length -lt 32) {
    throw 'Đã ghi user-secret nhưng bước xác minh thất bại.'
}

Write-Host 'Đã cấu hình JwtSettings:Secret an toàn trong .NET user-secrets.'
Write-Host 'Có thể chạy API bằng: dotnet run --launch-profile http'
