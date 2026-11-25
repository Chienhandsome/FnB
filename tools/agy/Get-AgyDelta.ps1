<#
.SYNOPSIS
  §4.2 của docs/workflow-agy.md: ghi ra file phần diff Claude chưa đọc, và lưu mốc đã đọc.
.DESCRIPTION
  Chụp worktree thành một tree bằng index tạm (không commit, không đụng index thật), rồi diff Kazuko_FnB/
  từ tree đã đọc lần trước (chưa có thì từ base). Chỉ khi Claude đã đọc hết mới chạy lại với -MarkRead <tree>.
.EXAMPLE
  .\tools\agy\Get-AgyDelta.ps1 -Feature luu-vet-hoat-dong
  .\tools\agy\Get-AgyDelta.ps1 -Feature luu-vet-hoat-dong -MarkRead <tree>
#>
param(
    [Parameter(Mandatory)][string]$Feature,
    [switch]$Full,
    [string]$MarkRead
)
. (Join-Path $PSScriptRoot 'AgyCommon.ps1')
$P = Get-FeaturePaths $Feature
if (-not (Test-Path $P.Worktree)) { throw "Chưa có worktree: $($P.Worktree)" }
$readFile = Join-Path $P.Tmp 'read-tree.txt'

if ($MarkRead) {
    Invoke-Git $P.Worktree 'cat-file' '-e' "$MarkRead^{tree}" | Out-Null
    Write-Utf8 $readFile $MarkRead
    Write-Output "Đã ghi mốc đã đọc: $MarkRead"
    return
}

# Lấy đường dẫn index thật TRƯỚC khi đặt GIT_INDEX_FILE.
$realIndex = (Invoke-Git $P.Worktree 'rev-parse' '--path-format=absolute' '--git-path' 'index')[0]
$tmpIndex = Join-Path $P.Tmp 'review.index'
Copy-Item $realIndex $tmpIndex -Force
try {
    $env:GIT_INDEX_FILE = $tmpIndex
    Invoke-Git $P.Worktree '-c' 'core.safecrlf=false' 'add' '-A' '--' 'Kazuko_FnB' | Out-Null
    $tree = (Invoke-Git $P.Worktree 'write-tree')[0]
} finally {
    Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue
    Remove-Item $tmpIndex -Force -ErrorAction SilentlyContinue
}

$from = (Read-Json $P.BaseFile).base
$fromLabel = 'base'
if (-not $Full -and (Test-Path $readFile)) {
    $from = (Get-Content $readFile -Raw).Trim()
    $fromLabel = 'tree đã đọc'
}
$out = Join-Path $P.Tmp 'delta.diff'
Invoke-Git $P.Worktree 'diff' "--output=$out" $from $tree '--' 'Kazuko_FnB' | Out-Null
$lines = @(Get-Content $out -Encoding UTF8).Count
Write-Output "Tree: $tree (so với $fromLabel $($from.Substring(0, 7)))"
if ($lines -eq 0) { Write-Output 'Không có gì mới phải đọc.'; return }
foreach ($l in (Invoke-Git $P.Worktree 'diff' '--stat' $from $tree '--' 'Kazuko_FnB')) { Write-Output $l }
Write-Output "Đọc hết $lines dòng trong $out, rồi chạy lại với -MarkRead $tree"
