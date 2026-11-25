<#
.SYNOPSIS
  §4 bước 5–6 của docs/workflow-agy.md (phần máy làm được): agy có ra khỏi phạm vi không, diff có đụng chỗ cấm không.
.DESCRIPTION
  Lỗi chặn (dừng, báo Chiến): thư mục chính của Chiến đổi; worktree có commit mới; file cấm bị sửa;
  .csproj thêm thứ ngoài <Compile Include>; file cấu hình chung của agy bị sửa; file ngoài worktree bị sửa.
  Cảnh báo (Claude đọc và quyết): file dùng chung bị sửa; mẫu code đáng ngờ trong dòng thêm mới.
  Không thay việc Claude đọc hết diff.
#>
param(
    [Parameter(Mandatory)][string]$Feature,
    [datetime]$Since,
    [string]$BeforeFile,
    [string]$OutFile
)
. (Join-Path $PSScriptRoot 'AgyCommon.ps1')
$P = Get-FeaturePaths $Feature
$base = (Read-Json $P.BaseFile).base
$blocking = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

# 1. Thư mục làm việc chính và worktree.
if ($BeforeFile -and (Test-Path $BeforeFile)) {
    $before = Read-Json $BeforeFile
    $head = (Invoke-Git $script:AgyRepo 'rev-parse' 'HEAD')[0]
    $status = @(Invoke-Git $script:AgyRepo 'status' '--porcelain')
    if ($head -ne $before.repoHead -or (($status -join '|') -ne (@($before.repoStatus) -join '|'))) {
        $blocking.Add("Thư mục chính $($script:AgyRepo) đã đổi trong lúc agy chạy (HEAD hoặc trạng thái file).")
    }
    $nowHash = $(if (Test-Path $script:AgyGlobalSettings) { (Get-FileHash -Algorithm SHA256 $script:AgyGlobalSettings).Hash } else { '' })
    if ($nowHash -ne $before.agySettingsHash) { $blocking.Add("File cấu hình chung của agy đã bị sửa: $($script:AgyGlobalSettings)") }
}
$wtHead = (Invoke-Git $P.Worktree 'rev-parse' 'HEAD')[0]
$expectedHead = $base
if (Test-Path (Join-Path $P.Tmp 'head.txt')) { $expectedHead = (Get-Content (Join-Path $P.Tmp 'head.txt') -Raw).Trim() }
if ($wtHead -ne $expectedHead) { $blocking.Add("Worktree có commit mới (HEAD $wtHead, mong đợi $expectedHead): agy không được commit.") }

# 2. File thay đổi.
$changed = New-Object System.Collections.Generic.List[string]
foreach ($l in (Invoke-Git $P.Worktree 'diff' '--name-only' 'HEAD')) { if ($l) { $changed.Add((Get-RelPath $l)) } }
foreach ($l in (Invoke-Git $P.Worktree 'ls-files' '--others' '--exclude-standard')) { if ($l) { $changed.Add((Get-RelPath $l)) } }
$shared = @()
foreach ($f in $changed) {
    if ($f -eq 'AGY-NOTES.md') { continue }
    $ext = [IO.Path]::GetExtension($f).ToLowerInvariant()
    if ((Test-PathPrefix $f $script:ForbiddenPrefixes) -or ($script:ForbiddenExtensions -contains $ext)) {
        $blocking.Add("File cấm bị sửa: $f")
    } elseif (-not $f.StartsWith('Kazuko_FnB/')) {
        $blocking.Add("File ngoài Kazuko_FnB/ bị sửa: $f")
    }
    if (Test-PathPrefix $f $script:SharedPrefixes) { $shared += $f }
}

# 3. .csproj: dòng thêm/bớt chỉ được là <Compile Include ...>, </Compile>, <SubType>, <DependentUpon>.
$csprojIssues = @()
foreach ($f in ($changed | Where-Object { $_ -like '*.csproj' })) {
    foreach ($l in (Invoke-Git $P.Worktree 'diff' '-U0' 'HEAD' '--' $f)) {
        if ($l -match '^(\+\+\+|---)') { continue }
        if ($l -match '^[+-]') {
            $body = $l.Substring(1).Trim()
            if ($body -eq '' -or $body -match '^<Compile Include="[^"]+\.cs"\s*/?>$' -or $body -match '^</Compile>$' -or
                $body -match '^<SubType>(Component|Form|UserControl)</SubType>$' -or $body -match '^<DependentUpon>[^<]+</DependentUpon>$') { continue }
            $csprojIssues += "$f : $body"
        }
    }
}
foreach ($c in $csprojIssues) { $blocking.Add(".csproj có thay đổi ngoài <Compile Include>: $c") }

# 4. Mẫu đáng ngờ trong dòng thêm mới (chỉ cảnh báo).
$suspicious = @()
$addedLines = @()
foreach ($l in (Invoke-Git $P.Worktree 'diff' '-U0' 'HEAD' '--' 'Kazuko_FnB')) { if ($l -match '^\+[^+]') { $addedLines += $l.Substring(1) } }
foreach ($f in (Invoke-Git $P.Worktree 'ls-files' '--others' '--exclude-standard' '--' 'Kazuko_FnB')) {
    if ($f -and $f -like '*.cs') { $addedLines += Get-Content (Join-Path $P.Worktree $f) -Encoding UTF8 }
}
foreach ($pat in $script:SuspiciousPatterns) {
    $hits = @($addedLines | Where-Object { $_ -match $pat })
    if ($hits.Count -gt 0) { $suspicious += ("{0} ({1} dòng), ví dụ: {2}" -f $pat, $hits.Count, $hits[0].Trim()) }
}

# 5. File bị sửa ngoài worktree kể từ lúc agy bắt đầu.
$outside = @()
if ($Since) {
    $wtPrefix = $P.Worktree.TrimEnd('\') + '\'
    foreach ($root in $script:OutsideScanRoots) {
        if (-not (Test-Path $root)) { continue }
        $rootItem = Get-Item $root -Force
        $items = $(if ($rootItem.PSIsContainer) { @(Get-ChildItem $root -Recurse -File -Force -ErrorAction SilentlyContinue) } else { @($rootItem) })
        foreach ($it in $items) {
            if ($it.PSIsContainer) { continue }
            if ($it.LastWriteTime -le $Since) { continue }
            $full = $it.FullName
            if ($full.StartsWith($wtPrefix, [StringComparison]::OrdinalIgnoreCase)) { continue }
            $skip = $false
            foreach ($ex in $script:OutsideScanExclude) { if ($full.IndexOf($ex, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $skip = $true; break } }
            if (-not $skip) { $outside += $full }
        }
    }
    # File log/tạm do phần mềm khác đang chạy tự ghi (đã gặp: agent.log của một app trong D:\Working\FnB): chỉ cảnh báo.
    foreach ($o in $outside) {
        $ext = [IO.Path]::GetExtension($o).ToLowerInvariant()
        if (@('.log', '.tmp', '.etl') -contains $ext) { $warnings.Add("File log/tạm ngoài worktree đổi trong lúc agy chạy: $o") }
        else { $blocking.Add("File ngoài worktree bị sửa trong lúc agy chạy: $o (kiểm xem có phải do việc khác của Chiến không)") }
    }
}

$result = [pscustomobject]@{
    feature = $Feature; ok = ($blocking.Count -eq 0); blocking = @($blocking); changedFiles = @($changed); sharedFiles = $shared
    suspicious = $suspicious; outsideChanged = $outside; warnings = @($warnings); checkedAt = (Get-Date -Format s)
}
foreach ($w in $warnings) { Write-Output "  [cảnh báo] $w" }
if ($OutFile) { Write-Json $OutFile $result }

Write-Output ("Phạm vi: {0} file thay đổi; dùng chung: {1}; đáng ngờ: {2}" -f $changed.Count, $shared.Count, $suspicious.Count)
foreach ($s in $shared) { Write-Output "  [dùng chung] $s" }
foreach ($s in $suspicious) { Write-Output "  [đáng ngờ] $s" }
if ($blocking.Count -gt 0) {
    foreach ($b in $blocking) { Write-Output "  [CHẶN] $b" }
    throw "Kiểm phạm vi KHÔNG ĐẠT ($($blocking.Count) lỗi chặn). Dừng, báo Chiến."
}
Write-Output 'Kiểm phạm vi: ĐẠT.'
