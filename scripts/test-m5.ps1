# Uji M5 — deep-discard: tab tidur lama DIBUANG (RAM turun), disentuh = hidup lagi (muat ulang)
param([string]$Exe = "src\Flit\bin\Release\net10.0-windows\Flit.exe")
$ErrorActionPreference = "Stop"
$Exe = (Resolve-Path $Exe).Path

$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $Exe
$psi.Arguments = "--mcp --allow-evaluate --sleep-after=5 --deep-after=15"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $false
$psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
$p = [System.Diagnostics.Process]::Start($psi)

function Recv([int]$sec = 45) {
    $t = $p.StandardOutput.ReadLineAsync()
    if (-not $t.Wait([TimeSpan]::FromSeconds($sec))) { return "<<TIMEOUT>>" }
    return $t.Result
}
function Snd($o) { $p.StandardInput.WriteLine(($o | ConvertTo-Json -Depth 10 -Compress)); $p.StandardInput.Flush() }
$script:id = 0
function CallFull($tool, $argsH, [int]$sec = 45) {
    $script:id++
    Snd @{ jsonrpc = "2.0"; id = $script:id; method = "tools/call"; params = @{ name = $tool; arguments = $argsH } }
    return Recv $sec
}
function RamOf($resp) {
    if ($resp -match 'RAM: (\d+) MB private') { $priv = $Matches[1] } else { return "(tidak terbaca)" }
    $ws = if ($resp -match '(\d+) MB WS') { $Matches[1] } else { "?" }
    return "private=$priv MB WS=$ws MB"
}

Snd @{ jsonrpc = "2.0"; id = 99; method = "initialize"; params = @{ protocolVersion = "2024-11-05"; capabilities = @{}; clientInfo = @{ name = "t"; version = "0" } } }
Recv | Out-Null

Start-Sleep -Seconds 2 # beri timer RAM (interval 3 dtk) kesempatan laporan pertama
$r = CallFull "flit_status" @{} 10
"RAM-AWAL: " + (RamOf $r)

# tab latar: index 1 (tidak diaktifkan)
CallFull "flit_new_tab" @{ url = "https://example.org"; activate = $false } 60 | Out-Null
Start-Sleep -Seconds 3
$r = CallFull "flit_status" @{} 10
"RAM-2TAB-HIDUP: " + (RamOf $r)
$r = CallFull "flit_list_tabs" @{} 10
# regex di-anchor ke url tab 1 (example.org) — isDiscarded tab 0 selalu false (tab aktif tak pernah dibuang)
if ($r -match 'example\.org[^\]]*?isDiscarded\\u0022:(\w+)') { "SEBELUM: isDiscarded=" + $Matches[1] }

"menunggu tidur (5 dtk) + dibuang (15 dtk) + sapuan..."
Start-Sleep -Seconds 45

$r = CallFull "flit_list_tabs" @{} 10
if ($r -match 'example\.org[^\]]*?isDiscarded\\u0022:(\w+)') { "SESUDAH: isDiscarded=" + $Matches[1] }
$r = CallFull "flit_status" @{} 10
"RAM-SETELAH-DIBUANG: " + (RamOf $r)

# sentuh tab yang dibuang → harus hidup lagi dan bisa dibaca
$r = CallFull "flit_evaluate_js" @{ script = "document.title + ' | ' + location.href"; tabIndex = 1; confirm = $true } 60
"EVAL-TAB-YANG-DIBUANG: " + $r
$r = CallFull "flit_list_tabs" @{} 10
if ($r -match 'example\.org[^\]]*?isDiscarded\\u0022:(\w+)') { "SETELAH-SENTUH: isDiscarded=" + $Matches[1] }

$p.StandardInput.Close(); Start-Sleep -Seconds 2
try { if (-not $p.HasExited) { $p.Kill($true) } } catch {}
"M5-TEST-DONE"
