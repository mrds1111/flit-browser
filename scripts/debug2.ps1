# Debug 2: siapa sebenarnya isi DOM tab aktif?
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
    return $t.Result
}
function Send($obj) { $p.StandardInput.WriteLine(($obj | ConvertTo-Json -Depth 10 -Compress)); $p.StandardInput.Flush() }
$id = 0
function Ev($script) {
    $script:id++
    Send @{ jsonrpc = "2.0"; id = $id; method = "tools/call"; params = @{ name = "flit_evaluate_js"; arguments = @{ script = $script } } }
    $r = Read-Response
    return "[$($script:id)] " + $r.Substring(0, [Math]::Min(280, $r.Length))
}

Send @{ jsonrpc = "2.0"; id = 99; method = "initialize"; params = @{ protocolVersion = "2024-11-05"; capabilities = @{}; clientInfo = @{ name = "dbg"; version = "0" } } }
Read-Response | Out-Null
Send @{ jsonrpc = "2.0"; id = 98; method = "tools/call"; params = @{ name = "flit_new_tab"; arguments = @{ url = "https://example.com"; activate = $true } } }
Read-Response 40 | Out-Null

Ev "document.title + ' | url=' + location.href" | Out-Host
Ev "document.querySelector('h1') ? document.querySelector('h1').textContent : 'NO-H1'" | Out-Host
Ev "document.body.children.length + ' children: ' + Array.from(document.body.children).map(e=>e.tagName).join(',')" | Out-Host
Ev "JSON.stringify(document.body.innerText.slice(0,120))" | Out-Host
Ev "var h=document.querySelector('h1'); h ? h.childNodes.length + '/' + JSON.stringify(h.textContent) : 'no h1'" | Out-Host

$p.StandardInput.Close()
Start-Sleep -Seconds 2
try { if (-not $p.HasExited) { $p.Kill($true) } } catch {}
"DEBUG2-DONE"
