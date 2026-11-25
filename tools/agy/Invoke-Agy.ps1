<#
.SYNOPSIS
  §4 bước 4 của docs/workflow-agy.md: chạy Antigravity CLI trong worktree của chức năng, rồi kiểm phạm vi ngay.
.DESCRIPTION
  Agy chỉ thấy worktree và thư mục agy-in (chứa prompt), dùng file quyền do New-AgyFeature.ps1 sinh ra.
  Vòng 1: không truyền -Conversation. Vòng sửa hoặc lượt bị dừng vì lệnh bị từ chối: truyền -Conversation và -Message.
  Có thể chạy lâu (vòng thử đầu khoảng 10 phút): nên chạy nền.
.EXAMPLE
  .\tools\agy\Invoke-Agy.ps1 -Feature luu-vet-hoat-dong -Round 1
  .\tools\agy\Invoke-Agy.ps1 -Feature luu-vet-hoat-dong -Round 2 -Conversation <id> -Message "Doc prompt-r2.md va sua theo do."
#>
param(
    [Parameter(Mandatory)][string]$Feature,
    [Parameter(Mandatory)][int]$Round,
    [string]$Conversation,
    [string]$Message,
    [string]$Model,
    [ValidateSet('accept-edits', 'plan')][string]$Mode = 'accept-edits',
    [int]$TimeoutMinutes = 60
)
. (Join-Path $PSScriptRoot 'AgyCommon.ps1')
$P = Get-FeaturePaths $Feature
if (-not (Test-Path $P.Worktree)) { throw "Chưa có worktree. Chạy New-AgyFeature.ps1 trước." }
$promptPath = Join-Path $P.AgyIn "prompt-r$Round.md"
if (-not $Message) {
    if (-not (Test-Path $promptPath)) { throw "Thiếu $promptPath" }
    if ((Get-Content $promptPath -Raw -Encoding UTF8) -match '<CLAUDE GHI') { throw "Prompt còn chỗ <CLAUDE GHI...> chưa điền: $promptPath" }
    # Không truyền nguyên prompt vào -p: PowerShell 5.1 cắt sai đối số có dấu ". Chỉ trỏ tới file.
    $Message = "Repo can sua nam o $($P.Worktree) (thu muc hien tai). Thu muc $($P.AgyIn) chi chua prompt. Doc file $promptPath (UTF-8) bang cong cu doc file va lam dung theo noi dung file do."
}
if ($Message -match '"') { throw 'Message không được chứa dấu " (PowerShell 5.1 cắt sai đối số).' }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDir = Join-Path $P.Tmp "run-r$Round-$stamp"
New-Item -ItemType Directory -Force $runDir | Out-Null
$before = [pscustomobject]@{
    time       = (Get-Date).ToString('o')
    repoHead   = (Invoke-Git $script:AgyRepo 'rev-parse' 'HEAD')[0]
    repoStatus = (Invoke-Git $script:AgyRepo 'status' '--porcelain')
    wtHead     = (Invoke-Git $P.Worktree 'rev-parse' 'HEAD')[0]
    # agy tự sửa file cấu hình chung của nó (nới quyền) phải bị phát hiện.
    agySettingsHash = $(if (Test-Path $script:AgyGlobalSettings) { (Get-FileHash -Algorithm SHA256 $script:AgyGlobalSettings).Hash } else { '' })
}
Write-Json (Join-Path $runDir 'before.json') $before

$args2 = @('-p', $Message, '--add-dir', $P.AgyIn, '--settings', $P.Settings, '--mode', $Mode, '--sandbox',
    '--log-file', (Join-Path $runDir 'agy.log'), '--print-timeout', "$($TimeoutMinutes)m", '--output-format', 'json')
if ($Conversation) { $args2 += @('--conversation', $Conversation) }
if ($Model) { $args2 += @('--model', $Model) }

# agy trả mã 0 cả khi lượt bị dừng vì lệnh bị từ chối, nên đọc JSON để biết kết quả.
$out = Invoke-Native -Exe $script:AgyExe -Arguments $args2 -WorkingDirectory $P.Worktree -AllowFail
$raw = $out -join "`n"
Write-Utf8 (Join-Path $runDir 'agy-out.txt') $raw
$i = $raw.IndexOf('{"conversation_id"')
if ($i -lt 0) { throw "Không đọc được JSON kết quả của agy. Xem $runDir\agy-out.txt" }
$res = $raw.Substring($i) | ConvertFrom-Json
$denied = @()
if ($res.PSObject.Properties['denied_actions']) { $denied = @($res.denied_actions | ForEach-Object { $_.action + ':' + $_.display_name }) }
if (-not $res.PSObject.Properties['response']) { $res | Add-Member -NotePropertyName response -NotePropertyValue '' }
$stoppedByDenial = $raw -match 'no output produced'

$summary = [pscustomobject]@{
    feature = $Feature; round = $Round; runDir = $runDir; conversation = $res.conversation_id; status = $res.status
    duration = $res.duration_seconds; denied = $denied; stoppedByDenial = $stoppedByDenial
    agyVersion = ((Invoke-Native -Exe $script:AgyExe -Arguments @('--version'))[0]); model = $(if ($Model) { $Model } else { 'mặc định' })
    response = $res.response
}
Write-Json (Join-Path $runDir 'agy-result.json') $summary
Copy-Item (Join-Path $runDir 'agy-result.json') (Join-Path $P.Tmp "agy-result-r$Round.json") -Force

Write-Output ("agy xong: {0}, {1}s, hội thoại {2}" -f $res.status, [int]$res.duration_seconds, $res.conversation_id)
if ($stoppedByDenial) { Write-Output ("CẢNH BÁO: lượt bị dừng vì bị từ chối: {0}. Hỏi agy lệnh gì (-Mode plan, cùng -Conversation), rồi cho làm tiếp." -f ($denied -join ', ')) }

& (Join-Path $PSScriptRoot 'Test-AgyScope.ps1') -Feature $Feature -Since ([datetime]$before.time) -BeforeFile (Join-Path $runDir 'before.json') -OutFile (Join-Path $P.Tmp "scope-r$Round.json")
