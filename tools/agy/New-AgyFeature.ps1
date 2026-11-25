<#
.SYNOPSIS
  §4 bước 1–2 của docs/workflow-agy.md: kiểm spec đã duyệt, tạo worktree, build + selftest mốc, sinh file quyền cho agy.
.EXAMPLE
  .\tools\agy\New-AgyFeature.ps1 -Feature luu-vet-hoat-dong -Spec docs/specs/luu-vet-hoat-dong.md
#>
param(
    [Parameter(Mandatory)][string]$Feature,
    [Parameter(Mandatory)][string]$Spec
)
. (Join-Path $PSScriptRoot 'AgyCommon.ps1')
$P = Get-FeaturePaths $Feature
$repo = $script:AgyRepo

Invoke-Git $repo 'fetch' '-q' 'origin' 'main' | Out-Null
$base = (Invoke-Git $repo 'rev-parse' 'origin/main')[0].Trim()

# 1. Spec trên origin/main phải ở trạng thái ĐÃ DUYỆT (dòng "- Trạng thái:" đầu file).
$specText = Invoke-Git $repo 'show' "origin/main:$((Get-RelPath $Spec))"
$statusLine = $specText | Where-Object { $_ -match '^- Trạng thái:' } | Select-Object -First 1
if (-not $statusLine -or $statusLine -notmatch 'ĐÃ DUYỆT') {
    throw "Spec '$Spec' chưa ĐÃ DUYỆT trên origin/main. Dòng trạng thái: $statusLine"
}

# 2. Worktree tách khỏi thư mục làm việc của Chiến.
if (Test-Path $P.Worktree) { throw "Worktree đã tồn tại: $($P.Worktree). Xem lại vòng trước hoặc xóa bằng Remove-AgyFeature.ps1." }
New-Item -ItemType Directory -Force $P.Tmp, $P.AgyIn, $P.Cache | Out-Null
Remove-Item (Join-Path $P.Tmp 'head.txt') -Force -ErrorAction SilentlyContinue
Invoke-Git $repo 'worktree' 'add' '--detach' $P.Worktree $base | Out-Null
Copy-Item -Recurse (Join-Path $repo 'Kazuko_FnB\lib') (Join-Path $P.Worktree 'Kazuko_FnB\lib') -Force
New-Item -ItemType Directory -Force (Join-Path $P.Worktree 'POS TAP\Data') | Out-Null
Copy-Item (Join-Path $repo 'POS TAP\Data\TEMPLATE.FDB') (Join-Path $P.Worktree 'POS TAP\Data\TEMPLATE.FDB')
$dirty = Invoke-Git $P.Worktree 'status' '--porcelain'
if (@($dirty | Where-Object { $_ }).Count -gt 0) { throw "Worktree mới không sạch sau khi chép lib/TEMPLATE: $($dirty -join '; ')" }

Write-Json $P.BaseFile ([pscustomobject]@{ feature = $Feature; spec = (Get-RelPath $Spec); base = $base; created = (Get-Date -Format s) })

# 3. Mốc build + selftest của base (lưu theo SHA để dùng lại).
$cacheDir = Join-Path $P.Cache $base
$baseJson = Join-Path $cacheDir 'baseline.json'
if (-not (Test-Path $baseJson)) {
    $b = Invoke-Build -Worktree $P.Worktree -OutDir (Join-Path $cacheDir 'build')
    if ($b.ExitCode -ne 0) { throw "Build của base $base lỗi; xem $($b.Log)" }
    $t = Invoke-SelfTest -ExePath $b.Exe -OutDir (Join-Path $cacheDir 'selftest')
    Write-Json $baseJson ([pscustomobject]@{ base = $base; warnings = $b.Warnings; total = $t.Total; pass = $t.Pass; fails = $t.Fails
        db = $script:SourceDb })
}
Copy-Item $baseJson (Join-Path $P.Tmp 'baseline.json') -Force

# 4. File quyền cho agy (chỉ dùng cho phiên, qua --settings). Đường dẫn PHẢI có ký tự ổ đĩa (D:/...) hoặc tương đối
#    theo thư mục làm việc; dạng bỏ ổ đĩa không có tác dụng (đã thử 25/11).
$repoFwd = ($repo -replace '\\', '/').TrimEnd('/') + '/'
# agy chạy --sandbox: quy tắc chặn phải là đường dẫn tuyệt đối, dạng tương đối làm MỌI lệnh shell bị dừng (25/11).
$wtFwd = ($P.Worktree -replace '\\', '/').TrimEnd('/') + '/'
$settings = [ordered]@{
    permissions = [ordered]@{
        allow = @(
            # Cho phép cả khi không có tham số (vòng 2 thử đầu bị dừng vì "Get-ChildItem" trơn).
            'command(regex:^(Get-Content|Get-ChildItem|Select-String|Test-Path|dir|ls|pwd|Get-Location)( [^|;&<>`]*)?$)',
            'command(regex:^git (status|diff|log|show|ls-files)( [^|;&<>`]*)?$)',
            # Chuỗi thay thế của -replace không coi '\' là ký tự đặc biệt: '\\' cho ra đúng hai ký tự '\\' (regex của một '\').
            ('command(regex:^& ?''' + ($script:MsBuildExe -replace '\\', '\\' -replace '\.', '\.') + ''' [^|;&<>`]*$)')
        )
        deny = @(
            "read_file($repoFwd)", "write_file($repoFwd)",
            "read_file(${wtFwd}POS TAP/)", "write_file(${wtFwd}POS TAP/)",
            "write_file(${wtFwd}docs/)", "write_file(${wtFwd}CLAUDE.md)", "write_file(${wtFwd}.gitignore)", "write_file(${wtFwd}.git)",
            "write_file(${wtFwd}Kazuko_FnB/lib/)", "write_file(${wtFwd}Kazuko_FnB/Kazuko_FnB.slnx)", "write_file(${wtFwd}tools/)",
            'command(regex:^git (commit|push|checkout|switch|reset|branch|worktree|stash|merge|rebase|clean|restore|tag|remote|apply|am|cherry-pick|revert).*)',
            'command(regex:.*(\.fdb|\.FDB|POS TAP|Remove-Item|rm |del |rmdir|Invoke-WebRequest|curl|wget|iwr|irm|Start-Process|Set-Content|Out-File|Copy-Item|Move-Item).*)'
        )
    }
}
Write-Json $P.Settings $settings

# 5. Prompt mẫu cho vòng 1 (Claude sửa phần "Việc" trước khi chạy Invoke-Agy.ps1).
$template = Get-Content (Join-Path $PSScriptRoot 'prompt-template.md') -Raw -Encoding UTF8
$prompt = $template.Replace('{SPEC}', (Get-RelPath $Spec)).Replace('{FEATURE}', $Feature).Replace('{BUILD_OUT}', (Join-Path $P.AgyIn 'build')).Replace('{WORKTREE}', $P.Worktree)
$promptPath = Join-Path $P.AgyIn 'prompt-r1.md'
if (-not (Test-Path $promptPath)) { Write-Utf8 $promptPath $prompt }

$bl = Read-Json (Join-Path $P.Tmp 'baseline.json')
Write-Output ("Đã tạo {0} @ {1}. Mốc: {2}; cảnh báo: {3}. Prompt: {4}" -f $P.Worktree, $base.Substring(0, 7), $bl.total, @($bl.warnings).Count, $promptPath)
