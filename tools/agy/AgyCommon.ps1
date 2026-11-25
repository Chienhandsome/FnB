# Hàm dùng chung cho các script tools/agy/*.ps1 (docs/workflow-agy.md).
# Chạy được trên Windows PowerShell 5.1. Mọi lệnh ngoài (git, MSBuild, agy, isql) đi qua Invoke-Native
# để DỪNG ngay khi trả mã lỗi (PowerShell không tự dừng với lệnh ngoài, kể cả khi ErrorActionPreference = Stop).

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'
# git và agy in ra UTF-8; PowerShell 5.1 mặc định đọc đầu ra lệnh ngoài theo bảng mã OEM nên tiếng Việt bị hỏng.
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
$OutputEncoding = [Console]::OutputEncoding
# Lưu ý: các file .ps1 này phải lưu UTF-8 CÓ BOM, nếu không PowerShell 5.1 đọc chữ Việt trong script sai.

$script:AgyRepo     = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:AgyWork     = 'D:\Working\fnb-agy-work'
$script:AgyExe      = Join-Path $env:LOCALAPPDATA 'agy\bin\agy.exe'
$script:MsBuildExe  = 'D:\VisualStudioInstallation\MSBuild\Current\Bin\MSBuild.exe'
$script:SourceDb    = Join-Path $script:AgyRepo 'POS TAP\Database\DATA.fdb'
$script:Utf8NoBom   = New-Object System.Text.UTF8Encoding $false
$script:AgyGlobalSettings = Join-Path $env:USERPROFILE '.gemini\antigravity-cli\settings.json'
# Nơi quét file bị sửa ngoài worktree sau mỗi lượt agy (ngoài các thư mục loại trừ bên dưới).
$script:OutsideScanRoots = @('D:\Working', (Join-Path $env:USERPROFILE 'Desktop'), (Join-Path $env:USERPROFILE 'Documents'),
    (Join-Path $env:USERPROFILE 'Downloads'), (Join-Path $env:USERPROFILE '.ssh'), (Join-Path $env:USERPROFILE '.gitconfig'))
$script:OutsideScanExclude = @('\.git\', '\.vs\', '\bin\', '\obj\', '\fnb-agy-work\_tmp\', '\fnb-agy-work\_cache\')

# File và thư mục agy không được sửa (§4 bước 6 của workflow). So khớp theo tiền tố đường dẫn trong repo.
$script:ForbiddenPrefixes = @('docs/', 'CLAUDE.md', '.gitignore', '.github/', '.claude/', 'Kazuko_FnB/lib/',
    'Kazuko_FnB/Kazuko_FnB.slnx', 'tools/', 'POS TAP/')
$script:ForbiddenExtensions = @('.ps1', '.bat', '.cmd', '.dll', '.exe', '.fdb', '.zip')
# File dùng chung: được sửa nhưng phải ghi riêng trong báo cáo.
$script:SharedPrefixes = @('Kazuko_FnB/Kazuko_FnB.UI/Theme/', 'Kazuko_FnB/Kazuko_FnB.UI/Diagnostics/SelfTest.cs',
    'Kazuko_FnB/Kazuko_FnB.UI/Dialogs/ScreenFactory.cs', 'Kazuko_FnB/Kazuko_FnB.UI/MainForm.cs',
    'Kazuko_FnB/Kazuko_FnB.UI/Controls/', 'Kazuko_FnB/Kazuko_FnB.Data/DbHelper.cs',
    'Kazuko_FnB/Kazuko_FnB.Services/SystemConfig.cs')
# Mẫu đáng ngờ trong dòng code thêm mới: chỉ báo, Claude đọc và quyết.
$script:SuspiciousPatterns = @('Process\.Start', 'WebClient', 'HttpClient', 'WebRequest', 'File\.Delete', 'Directory\.Delete',
    '\bCREATE\s+(TABLE|INDEX|TRIGGER|PROCEDURE)', '\bALTER\s+TABLE', '\bDROP\s+', 'POS TAP', 'DATA\.fdb', '\bdouble\b',
    'Color\.FromArgb', 'Color\.[A-Z][a-z]+\b', 'new Font\("')

function Invoke-Native {
    param([Parameter(Mandatory)][string]$Exe, [string[]]$Arguments = @(), [switch]$AllowFail, [string]$WorkingDirectory)
    $old = $null
    if ($WorkingDirectory) { $old = Get-Location; Set-Location $WorkingDirectory }
    # PowerShell 5.1: với ErrorActionPreference = Stop, dòng stderr của lệnh ngoài (2>&1) bị coi là lỗi và dừng script,
    # dù lệnh thành công (git ghi thông báo thường ra stderr). Tạm hạ xuống Continue, rồi phán theo mã thoát.
    $eap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = & $Exe @Arguments 2>&1 | ForEach-Object { "$_" }
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $eap
        if ($old) { Set-Location $old }
    }
    if ($code -ne 0 -and -not $AllowFail) {
        throw ("Lệnh lỗi (mã {0}): {1} {2}`n{3}" -f $code, $Exe, ($Arguments -join ' '), (($out | Select-Object -Last 30) -join "`n"))
    }
    return ,@($out)
}

function Invoke-Git {
    param([string]$Dir, [Parameter(ValueFromRemainingArguments)][string[]]$GitArgs)
    $r = Invoke-Native -Exe 'git' -Arguments (@('-C', $Dir) + $GitArgs)
    return ,@($r)
}

function Get-FeaturePaths {
    param([Parameter(Mandatory)][string]$Feature)
    if ($Feature -notmatch '^[a-z0-9][a-z0-9-]{1,60}$') { throw "Tên chức năng không hợp lệ: '$Feature' (chỉ a-z, 0-9, dấu -)." }
    $tmp = Join-Path $script:AgyWork "_tmp\$Feature"
    return [pscustomobject]@{
        Feature  = $Feature
        Worktree = Join-Path $script:AgyWork $Feature
        Tmp      = $tmp
        AgyIn    = Join-Path $tmp 'agy-in'
        Settings = Join-Path $tmp 'agy-settings.json'
        BaseFile = Join-Path $tmp 'base.txt'
        Cache    = Join-Path $script:AgyWork '_cache'
        State    = Join-Path $script:AgyWork 'state.txt'
    }
}

function Write-Utf8 { param([string]$Path, [string]$Text) [IO.File]::WriteAllText($Path, $Text, $script:Utf8NoBom) }

function Read-Json { param([string]$Path) return (Get-Content $Path -Raw -Encoding UTF8 | ConvertFrom-Json) }

function Write-Json { param([string]$Path, $Object) Write-Utf8 $Path ($Object | ConvertTo-Json -Depth 6) }

function Get-RelPath {
    param([string]$Path)
    return ($Path -replace '\\', '/').TrimStart('/')
}

function Test-PathPrefix {
    param([string]$RelPath, [string[]]$Prefixes)
    foreach ($p in $Prefixes) {
        if ($RelPath.StartsWith($p, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function Get-TreeFingerprint {
    # Dấu vân tay của mọi thay đổi chưa commit trong worktree (kể cả file mới), để biết code có đổi sau khi đã kiểm không.
    param([string]$Worktree)
    $diff = Invoke-Git $Worktree 'diff' 'HEAD' '--binary'
    $untracked = Invoke-Git $Worktree 'ls-files' '--others' '--exclude-standard'
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine(($diff -join "`n"))
    foreach ($f in ($untracked | Sort-Object)) {
        # AGY-NOTES.md không phải code và được chuyển ra _tmp trước khi commit.
        if ([string]::IsNullOrWhiteSpace($f) -or $f -eq 'AGY-NOTES.md') { continue }
        [void]$sb.AppendLine("== $f")
        [void]$sb.AppendLine((Get-FileHash -Algorithm SHA256 (Join-Path $Worktree $f)).Hash)
    }
    $bytes = [Text.Encoding]::UTF8.GetBytes($sb.ToString())
    $sha = [Security.Cryptography.SHA256]::Create()
    return ([BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '')
}

function Invoke-SelfTest {
    # Chép mới DATA.fdb vào thư mục kết quả rồi chạy --selftest. Không bao giờ chạy lên DB thật.
    param([string]$ExePath, [string]$OutDir)
    New-Item -ItemType Directory -Force $OutDir | Out-Null
    $dbCopy = Join-Path $OutDir 'selftest-db.fdb'
    $result = Join-Path $OutDir 'selftest.txt'
    Copy-Item $script:SourceDb $dbCopy -Force
    if (Test-Path $result) { Remove-Item $result -Force }
    $p = Start-Process -FilePath $ExePath -ArgumentList "--selftest `"$dbCopy`" `"$result`"" -Wait -PassThru -WindowStyle Hidden
    if (-not (Test-Path $result)) { throw "Selftest không tạo file kết quả (mã thoát $($p.ExitCode))." }
    # Ép về [string]: dòng của Get-Content mang thêm thuộc tính PSPath... làm JSON phình và so sánh sai.
    $lines = @(Get-Content $result -Encoding UTF8 | ForEach-Object { [string]$_ })
    Remove-Item $dbCopy -Force
    return [pscustomobject]@{
        Total = ($lines | Where-Object { $_ -like 'TỔNG*' } | Select-Object -Last 1)
        Pass  = @($lines | Where-Object { $_ -like 'PASS*' }).Count
        Fails = @($lines | Where-Object { $_ -like 'FAIL*' })
        File  = $result
    }
}

function Invoke-Build {
    # Build cấu hình mặc định ra thư mục riêng; trả về danh sách cảnh báo đã chuẩn hóa (bỏ đường dẫn).
    param([string]$Worktree, [string]$OutDir)
    New-Item -ItemType Directory -Force $OutDir | Out-Null
    $log = Join-Path (Split-Path $OutDir -Parent) ((Split-Path $OutDir -Leaf) + '.log')
    $out = Invoke-Native -Exe $script:MsBuildExe -AllowFail -Arguments @(
        (Join-Path $Worktree 'Kazuko_FnB\Kazuko_FnB.slnx'), '-nologo', '-v:m', "-p:OutDir=$OutDir\")
    $code = $LASTEXITCODE
    Write-Utf8 $log ($out -join "`r`n")
    $norm = {
        param($l) (($l -replace '\s+\[[^\]]*\]\s*$', '') -replace '^.*?:\s*(warning|error)\s', '$1 ').Trim()
    }
    $warnings = @($out | Where-Object { $_ -match ':\s*warning\s' } | ForEach-Object { & $norm $_ } | Sort-Object -Unique)
    $errors = @($out | Where-Object { $_ -match ':\s*error\s' } | ForEach-Object { & $norm $_ } | Sort-Object -Unique)
    return [pscustomobject]@{ ExitCode = $code; Warnings = $warnings; Errors = $errors; Log = $log
        Exe = (Join-Path $OutDir 'Kazuko_FnB.exe') }
}
