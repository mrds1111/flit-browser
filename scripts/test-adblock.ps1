# Uji adblock M3 — network blocking + cosmetic injection
param([string]$Exe = "src\Flit\bin\Release\net10.0-windows\Flit.exe")
$ErrorActionPreference = "Stop"
$Exe = (Resolve-Path $Exe).Path

$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $Exe
$psi.Arguments = "--mcp --allow-evaluate"
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
$id = 0
function Call($tool, $argsH, [int]$sec = 45) {
    $script:id++
    Snd @{ jsonrpc = "2.0"; id = $id; method = "tools/call"; params = @{ name = $tool; arguments = $argsH } }
    $resp = Recv $sec
    return "[$($script:id)] $tool : " + $resp.Substring(0, [Math]::Min(400, $resp.Length))
}
function CallFull($tool, $argsH, [int]$sec = 45) {
    $script:id++
    Snd @{ jsonrpc = "2.0"; id = $id; method = "tools/call"; params = @{ name = $tool; arguments = $argsH } }
    return Recv $sec
}

Snd @{ jsonrpc = "2.0"; id = 99; method = "initialize"; params = @{ protocolVersion = "2024-11-05"; capabilities = @{}; clientInfo = @{ name = "t"; version = "0" } } }
Recv | Out-Null

# tunggu daftar filter diunduh + di-parse di background
"menunggu daftar filter (unduh + parse)..."
$r = ""
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Seconds 3
    $r = CallFull "flit_status" @{} 10
    if ($r -match 'blocked (\d+)') { if ([int]$Matches[1] -gt 0) { break } }
}
"STATUS-AWAL: " + $r

# navigasi situs ramah-iklan (detik.com) — counter blocked harus naik
Call "flit_new_tab" @{ url = "https://www.detik.com"; activate = $true } 60 | Out-Host
Start-Sleep -Seconds 5
$r = CallFull "flit_status" @{} 10
if ($r -match 'blocked (\d+)') { "BLOCKED-AFTER-DETIK: " + $Matches[1] } else { "BLOCKED-AFTER-DETIK: (tidak ada field) " + $r.Substring(0, [Math]::Min(300, $r.Length)) }

# cosmetic: style[data-flit] harus terpasang di dokumen
CallFull "flit_evaluate_js" @{ script = "document.querySelectorAll('style[data-flit]').length" } | Out-Host

$p.StandardInput.Close(); Start-Sleep -Seconds 2
try { if (-not $p.HasExited) { $p.Kill($true) } } catch {}
"ADBLOCK-TEST-DONE"
