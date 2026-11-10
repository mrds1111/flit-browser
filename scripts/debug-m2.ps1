# Debug M2: temuan count:0 + screenshot hang — probe via evaluate_js + varian CDP
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

function Read-Response([int]$sec = 25) {
    $t = $p.StandardOutput.ReadLineAsync()
    if (-not $t.Wait([TimeSpan]::FromSeconds($sec))) { return "<<TIMEOUT>>" }
    if ($null -eq $t.Result) { throw "stdout closed" }
    return $t.Result
}
function Send($obj) { $p.StandardInput.WriteLine(($obj | ConvertTo-Json -Depth 10 -Compress)); $p.StandardInput.Flush() }
$id = 0
function Eval($script) {
    $script:id++
    Send @{ jsonrpc = "2.0"; id = $id; method = "tools/call"; params = @{ name = "flit_evaluate_js"; arguments = @{ script = $script } } }
    $r = Read-Response
    return "[$($script:id)] " + $r.Substring(0, [Math]::Min(220, $r.Length))
}
function Call($tool, $argsH) {
    $script:id++
    Send @{ jsonrpc = "2.0"; id = $id; method = "tools/call"; params = @{ name = $tool; arguments = $argsH } }
    $r = Read-Response 30
    return "[$($script:id)] $tool : " + $r.Substring(0, [Math]::Min(200, $r.Length))
}

Send @{ jsonrpc = "2.0"; id = 99; method = "initialize"; params = @{ protocolVersion = "2024-11-05"; capabilities = @{}; clientInfo = @{ name = "dbg"; version = "0" } } }
Read-Response | Out-Null
Call "flit_new_tab" @{ url = "https://example.com"; activate = $true } | Out-Null

Eval "document.body.innerText.toLowerCase().indexOf('example domain')" | Out-Host
Eval "typeof document.createTreeWalker + '/' + (document.body ? 'body-ok' : 'no-body')" | Out-Host
Eval "var c=0;var w=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT,null);var n;while(n=w.nextNode()){c++} 'textNodes='+c" | Out-Host
Eval "var c=0;var w=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT,null);var n;var qq='example domain';while(n=w.nextNode()){var t=(n.textContent||'').toLowerCase();if(t.indexOf(qq)!==-1){c++}} 'matchNodes='+c" | Out-Host
Call "flit_find" @{ query = "Example" } | Out-Host

Call "flit_screenshot" @{ } | Out-Host
Call "flit_snapshot" @{ maxNodes = 30 } | Out-Host

$p.StandardInput.Close()
Start-Sleep -Seconds 2
try { if (-not $p.HasExited) { $p.Kill($true) } } catch {}
"DEBUG-DONE"
