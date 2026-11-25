<#
.SYNOPSIS
  §4 bước 13 của docs/workflow-agy.md: xóa worktree của chức năng (giữ _tmp để tra lại).
  Từ chối nếu worktree còn thay đổi chưa commit, trừ khi có -Discard (bỏ code của vòng bị dừng).
#>
param(
    [Parameter(Mandatory)][string]$Feature,
    [switch]$Discard
)
. (Join-Path $PSScriptRoot 'AgyCommon.ps1')
$P = Get-FeaturePaths $Feature
if (-not (Test-Path $P.Worktree)) { Write-Output 'Không có worktree.'; return }
# lib/ và POS TAP/ là file bị git bỏ qua (chép vào để build), không tính là thay đổi.
$pending = @((Invoke-Git $P.Worktree 'status' '--porcelain') | Where-Object { $_ })
if ($pending.Count -gt 0 -and -not $Discard) { throw "Worktree còn thay đổi chưa commit: $($pending -join '; '). Dùng -Discard nếu muốn bỏ." }
Invoke-Git $script:AgyRepo 'worktree' 'remove' '--force' $P.Worktree | Out-Null
Remove-Item (Join-Path $P.Tmp 'head.txt') -Force -ErrorAction SilentlyContinue
Write-Output "Đã xóa worktree $($P.Worktree)."
