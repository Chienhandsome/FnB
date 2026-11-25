<#
.SYNOPSIS
  §4 bước 7 của docs/workflow-agy.md: build worktree, so cảnh báo với mốc, chạy selftest trên bản chép DB, so ca lỗi với mốc.
.DESCRIPTION
  Đạt khi: build 0 lỗi, không cảnh báo mới, không ca selftest lỗi mới, có ít nhất một ca selftest mới,
  và không có file .cs mới (hoặc file đang <= 500 dòng) vượt 500 dòng. File cũ đã quá 500 mà dài thêm: chỉ báo.
  Ghi build-r<N>.json kèm dấu vân tay của code đã kiểm; Publish-AgyFeature.ps1 từ chối push nếu code đổi sau đó.
#>
param(
    [Parameter(Mandatory)][string]$Feature,
    [Parameter(Mandatory)][int]$Round
)
. (Join-Path $PSScriptRoot 'AgyCommon.ps1')
$P = Get-FeaturePaths $Feature
$baseline = Read-Json (Join-Path $P.Tmp 'baseline.json')
$fingerprint = Get-TreeFingerprint $P.Worktree

$b = Invoke-Build -Worktree $P.Worktree -OutDir (Join-Path $P.Tmp "build-r$Round")
$newWarnings = @($b.Warnings | Where-Object { @($baseline.warnings) -notcontains $_ })
$problems = @()
if ($b.ExitCode -ne 0 -or $b.Errors.Count -gt 0) { $problems += "Build lỗi: $($b.Errors -join ' | ') (xem $($b.Log))" }
if ($newWarnings.Count -gt 0) { $problems += "Cảnh báo mới: $($newWarnings -join ' | ')" }

# Độ dài file .cs đã đổi (bỏ *.Designer.cs).
$maxLines = 500
$sizeNotes = @()
# Không bọc @( ) quanh Invoke-Git: hàm đã trả mảng, bọc thêm thành mảng lồng mảng.
$tracked = Invoke-Git $P.Worktree 'diff' '--name-only' '--diff-filter=AMR' 'HEAD' '--' 'Kazuko_FnB'
$untracked = Invoke-Git $P.Worktree 'ls-files' '--others' '--exclude-standard' '--' 'Kazuko_FnB'
foreach ($f in @($tracked + $untracked | Where-Object { $_ -like '*.cs' -and $_ -notlike '*.Designer.cs' } | Sort-Object -Unique)) {
    $now = @(Get-Content (Join-Path $P.Worktree $f) -Encoding UTF8).Count
    if ($now -le $maxLines) { continue }
    $old = 0
    $inHead = Invoke-Git $P.Worktree 'ls-tree' '--name-only' 'HEAD' '--' $f
    if ($inHead.Count -gt 0) { $old = (Invoke-Git $P.Worktree 'show' "HEAD:$f").Count }
    if ($old -le $maxLines) { $problems += "File quá $maxLines dòng: $f ($old -> $now)" }
    elseif ($now -gt $old) { $sizeNotes += "File cũ đã quá $maxLines dòng và dài thêm: $f ($old -> $now)" }
}

$t = $null; $newFails = @(); $newPass = 0
if ($b.ExitCode -eq 0) {
    $t = Invoke-SelfTest -ExePath $b.Exe -OutDir (Join-Path $P.Tmp "selftest-r$Round")
    $newFails = @($t.Fails | Where-Object { @($baseline.fails) -notcontains $_ })
    $newPass = $t.Pass - [int]$baseline.pass
    if ($newFails.Count -gt 0) { $problems += "Ca selftest lỗi mới: $($newFails -join ' | ')" }
    if ($newPass -le 0) { $problems += 'Không có ca selftest mới nào đạt (chức năng phải có selftest).' }
    $gone = @($baseline.fails | Where-Object { @($t.Fails) -notcontains $_ })
}

$result = [pscustomobject]@{
    feature = $Feature; round = $Round; ok = ($problems.Count -eq 0); problems = $problems; fingerprint = $fingerprint
    base = $baseline.base; baseTotal = $baseline.total; total = $(if ($t) { $t.Total } else { '' })
    newPass = $newPass; newFails = $newFails; baseFailsStillFailing = $(if ($t) { @($baseline.fails).Count - @($gone).Count } else { 0 })
    warnings = $b.Warnings; newWarnings = $newWarnings; sizeNotes = $sizeNotes; selftestFile = $(if ($t) { $t.File } else { '' })
    exe = $b.Exe; checkedAt = (Get-Date -Format s)
}
Write-Json (Join-Path $P.Tmp "build-r$Round.json") $result

Write-Output ("Build: mã {0}, lỗi {1}, cảnh báo mới {2}" -f $b.ExitCode, $b.Errors.Count, $newWarnings.Count)
if ($t) { Write-Output ("Selftest: mốc '{0}' -> '{1}'; ca đạt mới {2}; ca lỗi mới {3}" -f $baseline.total, $t.Total, $newPass, $newFails.Count) }
foreach ($n in $sizeNotes) { Write-Output "  [cảnh báo] $n" }
if ($problems.Count -gt 0) {
    foreach ($p in $problems) { Write-Output "  [LỖI] $p" }
    throw "Build/selftest KHÔNG ĐẠT."
}
Write-Output 'Build và selftest: ĐẠT.'
