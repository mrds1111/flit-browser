# Uji end-to-end server MCP Flit (M2) — pakai:  .\scripts\test-mcp.ps1
param(
    [string]$Exe = "src\Flit\bin\Release\net10.0-windows\Flit.exe"
)
$ErrorActionPreference = "Stop"
$Exe = (Resolve-Path $Exe).Path

$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = $Exe
$psi.Arguments = "--mcp --allow-evaluate"
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $false   # jangan pipakan stderr — hindari deadlock pipe
$psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8

$p = [System.Diagnostics.Process]::Start($psi)

function Read-Response([int]$sec = 90) {
    $t = $p.StandardOutput.ReadLineAsync()
    if (-not $t.Wait([TimeSpan]::FromSeconds($sec))) { throw "Timeout menunggu respons MCP" }
    if ($null -eq $t.Result) { throw "Stdout ditutup oleh Flit" }
    return $t.Result
}

function Send($obj) {
    $p.StandardInput.WriteLine(($obj | ConvertTo-Json -Depth 10 -Compress))
    $p.StandardInput.Flush()
}

$reqId = 0
function Call($tool, $argsH) {
    $script:reqId++
    Send @{ jsonrpc = "2.0"; id = $reqId; method = "tools/call"; params = @{ name = $tool; arguments = $argsH } }
    return Read-Response
}

Send @{ jsonrpc = "2.0"; id = 1; method = "initialize"; params = @{ protocolVersion = "2024-11-05"; capabilities = @{}; clientInfo = @{ name = "flit-test"; version = "0" } } }
"INIT     : " + (Read-Response).Substring(0, 140)

$r = Call "flit_new_tab" @{ url = "https://example.com"; activate = $true }
"NEW_TAB  : " + $r.Substring(0, [Math]::Min(120, $r.Length))

$r = Call "flit_get_page_text" @{}
"TEXT     : " + $r.Substring(0, [Math]::Min(170, $r.Length))

$r = Call "flit_evaluate_js" @{ script = "navigator.webdriver" }
"WEBDRIVER: " + $r

$r = Call "flit_find" @{ query = "domain" }
"FIND     : " + $r

$r = Call "flit_screenshot" @{}
"SHOT     : " + $r

$r = Call "flit_list_tabs" @{}
"TABS     : " + $r.Substring(0, [Math]::Min(240, $r.Length))

$r = Call "flit_status" @{}
"STATUS   : " + $r

$p.StandardInput.Close()
$sw = [System.Diagnostics.Stopwatch]::StartNew()
while (-not $p.HasExited -and $sw.Elapsed.TotalSeconds -lt 15) { Start-Sleep -Milliseconds 200 }
"EXIT     : HasExited=$($p.HasExited) Code=$(if ($p.HasExited) { $p.ExitCode } else { 'masih hidup' })"
try { if (-not $p.HasExited) { $p.Kill($true) } } catch {}
"MCP-TEST-DONE"
