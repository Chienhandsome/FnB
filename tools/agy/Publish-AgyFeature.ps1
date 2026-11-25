<#
.SYNOPSIS
  §4 bước 11 của docs/workflow-agy.md: commit code agy đã qua kiểm, push nối tiếp lên main, ghi state.txt.
.DESCRIPTION
  Chỉ chạy sau khi Claude đã ĐỌC HẾT diff (kể cả selftest) và Test-AgyScope.ps1, Test-AgyBuild.ps1 đều đạt.
  Từ chối nếu: code đổi sau lần Test-AgyBuild cuối; còn lỗi phạm vi; origin/main đã đổi ngoài docs/.
  origin/main chỉ đổi trong docs/: rebase rồi dừng, yêu cầu chạy lại Test-AgyBuild.ps1 trước khi push.
.EXAMPLE
  .\tools\agy\Publish-AgyFeature.ps1 -Feature luu-vet-hoat-dong -Round 2 -Title "luu-vet-hoat-dong r2: sua cat chu dong dam" -CoAuthor "Claude Sonnet 5.5" -ReviewedFullDiff
#>
param(
    [Parameter(Mandatory)][string]$Feature,
    [Parameter(Mandatory)][int]$Round,
    [Parameter(Mandatory)][string]$Title,
    # Tên model Claude của phiên đã kiểm, ghi vào dòng Co-Authored-By.
    [Parameter(Mandatory)][string]$CoAuthor,
    [string]$Body = '',
    [switch]$ReviewedFullDiff
)
. (Join-Path $PSScriptRoot 'AgyCommon.ps1')
$P = Get-FeaturePaths $Feature
if (-not $ReviewedFullDiff) { throw 'Thiếu -ReviewedFullDiff: Claude phải đọc hết diff (kể cả selftest) trước khi push.' }
if ($Title -match '"' -or $Body -match '"') { throw 'Title/Body không được chứa dấu ".' }
if ($CoAuthor -notmatch '^Claude [A-Za-z0-9 .]+$') { throw "CoAuthor phải là tên model Claude, ví dụ Claude Sonnet 5.5 (nhận: $CoAuthor)." }

$baseInfo = Read-Json $P.BaseFile
$build = Read-Json (Join-Path $P.Tmp "build-r$Round.json")
$agy = Read-Json (Join-Path $P.Tmp "agy-result-r$Round.json")
if (-not $build.ok) { throw "Test-AgyBuild vòng $Round chưa đạt." }

# Ghi chú của agy: chép ra _tmp, không commit.
$notes = Join-Path $P.Worktree 'AGY-NOTES.md'
if (Test-Path $notes) { Move-Item $notes (Join-Path $P.Tmp "AGY-NOTES-r$Round.md") -Force }

& (Join-Path $PSScriptRoot 'Test-AgyScope.ps1') -Feature $Feature -OutFile (Join-Path $P.Tmp "scope-publish-r$Round.json")
if ((Get-TreeFingerprint $P.Worktree) -ne $build.fingerprint) { throw 'Code trong worktree đã đổi sau lần Test-AgyBuild cuối. Chạy lại Test-AgyBuild.ps1.' }

Invoke-Git $P.Worktree 'fetch' '-q' 'origin' 'main' | Out-Null
$remote = (Invoke-Git $P.Worktree 'rev-parse' 'origin/main')[0]
$head = (Invoke-Git $P.Worktree 'rev-parse' 'HEAD')[0]
if ($remote -ne $head) {
    # Không bọc @( ) quanh Invoke-Git: hàm đã trả mảng, bọc thêm thành mảng lồng mảng và bộ lọc dưới bỏ sót file code.
    $moved = Invoke-Git $P.Worktree 'diff' '--name-only' $head $remote
    $nonDocs = @($moved | Where-Object { $_ -and -not $_.StartsWith('docs/') })
    if ($nonDocs.Count -gt 0) { throw "origin/main đã đổi code từ $($head.Substring(0,7)): $($nonDocs -join ', '). Dừng; giao lại agy từ main mới." }
    # Chỉ docs/ đổi: đưa worktree lên main mới (stash các thay đổi chưa commit, checkout, áp lại), rồi bắt kiểm lại.
    Invoke-Git $P.Worktree 'stash' 'push' '--include-untracked' '-m' 'agy-publish' | Out-Null
    Invoke-Git $P.Worktree 'checkout' '--detach' $remote | Out-Null
    Invoke-Git $P.Worktree 'stash' 'pop' | Out-Null
    Write-Utf8 (Join-Path $P.Tmp 'head.txt') $remote
    throw "origin/main chỉ đổi trong docs/; worktree đã chuyển lên $($remote.Substring(0,7)). Chạy lại Test-AgyBuild.ps1 rồi Publish."
}

$msg = @"
feat: $Title

$Body

Spec: $($baseInfo.spec)
Base: $($head.Substring(0,7)); build: $($build.total) (mốc $($build.baseTotal)), ca mới $($build.newPass), ca lỗi mới 0
Generated-By: Antigravity CLI $($agy.agyVersion) ($($agy.model))
Agy-Conversation: $($agy.conversation)
Reviewed-By: Claude (đọc diff, build, selftest theo docs/workflow-agy.md)

Co-Authored-By: $CoAuthor <noreply@anthropic.com>
"@
$msgFile = Join-Path $P.Tmp "commit-msg-r$Round.txt"
Write-Utf8 $msgFile $msg
Invoke-Git $P.Worktree 'add' '--' 'Kazuko_FnB' | Out-Null
$staged = @((Invoke-Git $P.Worktree 'diff' '--cached' '--name-only') | Where-Object { $_ })
if ($staged.Count -eq 0) { throw 'Không có gì để commit.' }
$other = @((Invoke-Git $P.Worktree 'status' '--porcelain') | Where-Object { $_ -and $_ -notmatch '^[AMDR]  ' })
if ($other.Count -gt 0) { throw "Còn thay đổi ngoài Kazuko_FnB/ chưa xử lý: $($other -join '; ')" }
Invoke-Git $P.Worktree 'commit' '-q' '-F' $msgFile | Out-Null
$commit = (Invoke-Git $P.Worktree 'rev-parse' 'HEAD')[0]

# Push nối tiếp, không --force. Bị từ chối (main vừa đổi) thì dừng, không tự xử lý.
Invoke-Git $P.Worktree 'push' 'origin' 'HEAD:refs/heads/main' | Out-Null
Write-Utf8 (Join-Path $P.Tmp 'head.txt') $commit

Add-Content -Path $P.State -Encoding UTF8 -Value ("{0} r{1} ok {2} {3} {4}" -f $Feature, $Round, $commit.Substring(0, 7), (Get-Date -Format s), $agy.conversation)
Write-Output "Đã push $($commit.Substring(0,7)) lên main."
