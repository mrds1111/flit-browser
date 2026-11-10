# Uji M4 — S2 identitas koheren, profil terpisah, gerbang confirm, proxy
param([string]$Exe = "src\Flit\bin\Release\net10.0-windows\Flit.exe")
$ErrorActionPreference = "Stop"
$Exe = (Resolve-Path $Exe).Path
$UA = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"

$script:p = $null
function Recv([int]$sec = 45) {
    $t = $script:p.StandardOutput.ReadLineAsync()
    if (-not $t.Wait([TimeSpan]::FromSeconds($sec))) { return "<<TIMEOUT>>" }
    return $t.Result
}
function Snd($o) { $script:p.StandardInput.WriteLine(($o | ConvertTo-Json -Depth 10 -Compress)); $script:p.StandardInput.Flush() }
$script:id = 0
function CallFull($tool, $argsH, [int]$sec = 45) {
    $script:id++
    Snd @{ jsonrpc = "2.0"; id = $script:id; method = "tools/call"; params = @{ name = $tool; arguments = $argsH } }
    return Recv $sec
}
function Start-Flit([string]$extra) {
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $Exe
    $psi.Arguments = "--mcp --allow-evaluate $extra"
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $false
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $script:p = [System.Diagnostics.Process]::Start($psi)
    Snd @{ jsonrpc = "2.0"; id = 99; method = "initialize"; params = @{ protocolVersion = "2024-11-05"; capabilities = @{}; clientInfo = @{ name = "t"; version = "0" } } }
    Recv | Out-Null
}
function Stop-Flit {
    try { $script:p.StandardInput.Close(); Start-Sleep -Seconds 2; if (-not $script:p.HasExited) { $script:p.Kill($true) } } catch {}
    $script:p = $null
}
function Ev($js, [int]$sec = 30) { CallFull "flit_evaluate_js" @{ script = $js; confirm = $true } $sec }

"=== FASE A: S2 identitas koheren (UA Mac + lang ja-JP + TZ Europe/Paris, profil m4test) ==="
Start-Flit ('--profile=m4test --lang=ja-JP --timezone=Europe/Paris --ua="' + $UA + '"')
CallFull "flit_new_tab" @{ url = "https://example.com"; activate = $true } 60 | Out-Null
Start-Sleep -Seconds 3
Ev "JSON.stringify({ua: navigator.userAgent.indexOf('Macintosh')>=0, platform: navigator.platform, lang: navigator.language, langs: navigator.languages.join(','), tz: Intl.DateTimeFormat().resolvedOptions().timeZone, off: new Date().getTimezoneOffset(), cores: navigator.hardwareConcurrency, mem: navigator.deviceMemory, webdriver: navigator.webdriver})" | Out-Host
Ev "localStorage.setItem('flitM4','x1'); 'tersimpan'" | Out-Host
Stop-Flit

"=== FASE D: profil terpisah — nilai localStorage harus persist di profil m4test ==="
Start-Flit ('--profile=m4test --lang=ja-JP --timezone=Europe/Paris --ua="' + $UA + '"')
CallFull "flit_new_tab" @{ url = "https://example.com"; activate = $true } 60 | Out-Null
Start-Sleep -Seconds 3
Ev "localStorage.getItem('flitM4')" | Out-Host
Stop-Flit

"=== FASE B: gerbang confirm (--require-confirm) ==="
Start-Flit "--require-confirm"
CallFull "flit_evaluate_js" @{ script = "1+1" } 20 | Out-Host
CallFull "flit_evaluate_js" @{ script = "1+1"; confirm = $true } 20 | Out-Host
CallFull "flit_new_tab" @{ url = "about:blank"; activate = $true } 30 | Out-Null
CallFull "flit_close_tab" @{ index = 1 } 20 | Out-Host
CallFull "flit_close_tab" @{ index = 1; confirm = $true } 20 | Out-Host
Stop-Flit

"=== FASE C: proxy mati (127.0.0.1:9) — chrome://version harus menampilkan --proxy-server ==="
Start-Flit "--proxy=http://127.0.0.1:9"
CallFull "flit_new_tab" @{ url = "chrome://version"; activate = $true } 60 | Out-Null
Start-Sleep -Seconds 2
$r = CallFull "flit_get_page_text" @{ maxChars = 4000 } 30
if ($r -match 'proxy-server[^\s"\\]*') { "PROXY-DI-CHROMIUM: " + $Matches[0] } else { "PROXY-TIDAK-ADA-DI-CMDLINE: " + $r.Substring(0, [Math]::Min(300, $r.Length)) }
Stop-Flit

"M4-TEST-DONE"
