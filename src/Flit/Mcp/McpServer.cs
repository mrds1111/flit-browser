using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Flit.Mcp;

/// <summary>Galat tingkat tool → dilaporkan sebagai result isError=true (bukan error protokol).</summary>
public class McpToolError : Exception
{
    public McpToolError(string message) : base(message) { }
}

/// <summary>
/// Server MCP (Model Context Protocol) — transport stdio, JSON-RPC 2.0 newline-delimited.
/// Dijalankan lewat `Flit.exe --mcp` (jendela tidak pernah ditampilkan = headless).
/// Protokol loop jalan di thread sendiri; eksekusi tool dialihkan ke UI thread
/// (WebView2 satu apartment dengan WinForms).
/// </summary>
public class McpServer
{
    private const string ServerVersion = "0.6.0";
    private static readonly string[] KnownProtocolVersions = { "2024-11-05", "2025-03-26", "2025-06-18" };

    private readonly MainForm _form;
    private readonly AgentConfig _cfg;
    private readonly object _writeLock = new();

    public McpServer(MainForm form, AgentConfig cfg)
    {
        _form = form;
        _cfg = cfg;
    }

    public async Task RunAsync()
    {
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();
        using var reader = new StreamReader(stdin, new UTF8Encoding(false));

        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                await HandleLineAsync(line, stdout);
            }
            catch (McpToolError ex)
            {
                AgentLog.Write("mcp.tool_error", ex.Message);
                // galat tool yang lolos dari lapisan dispatch — kirim sebagai isError bila ada id
            }
            catch (Exception ex)
            {
                AgentLog.Write("mcp.internal_error", ex.Message);
            }
        }
        // stdin EOF → klien menutup sesi; biarkan RunAsync selesai, pemanggil yang menghentikan aplikasi
    }

    private async Task HandleLineAsync(string line, Stream stdout)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); }
        catch
        {
            WriteJson(stdout, new { jsonrpc = "2.0", id = (int?)null, error = new { code = -32700, message = "Parse error" } });
            return;
        }

        using (doc)
        {
            var root = doc.RootElement;
            var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
            var hasId = root.TryGetProperty("id", out var idEl);
            var id = hasId ? idEl.Clone() : (JsonElement?)null;

            // Notifikasi (tanpa id) → tidak dijawab
            if (!hasId)
            {
                AgentLog.Write("mcp.notification", method);
                return;
            }

            try
            {
                var result = method switch
                {
                    "initialize" => InitializeResult(root),
                    "ping" => (object)new { },
                    "tools/list" => ToolsList(),
                    "tools/call" => await ToolsCallAsync(root),
                    _ => throw new McpToolError($"Method tidak dikenal: {method}"),
                };
                WriteJson(stdout, new { jsonrpc = "2.0", id, result });
            }
            catch (McpToolError ex)
            {
                // Method-level unknown → error protokol; tool-level ditangani di ToolsCallAsync
                if (method == "tools/call")
                    WriteJson(stdout, new { jsonrpc = "2.0", id, error = new { code = -32603, message = ex.Message } });
                else
                    WriteJson(stdout, new { jsonrpc = "2.0", id, error = new { code = -32601, message = ex.Message } });
                AgentLog.Write("mcp.error", new { method, message = ex.Message });
            }
            catch (Exception ex)
            {
                WriteJson(stdout, new { jsonrpc = "2.0", id, error = new { code = -32603, message = ex.Message } });
                AgentLog.Write("mcp.error", new { method, message = ex.Message });
            }
        }
    }

    private static object InitializeResult(JsonElement req)
    {
        // Balas versi klien bila kami mengenalnya; kalau tidak, pakai versi terbaru yang dikenal
        var clientVer = "2024-11-05";
        try
        {
            if (req.TryGetProperty("params", out var p) &&
                p.TryGetProperty("protocolVersion", out var v) && v.ValueKind == JsonValueKind.String)
            {
                var s = v.GetString();
                if (s is not null && KnownProtocolVersions.Contains(s)) clientVer = s;
                else if (s is not null) clientVer = "2025-06-18";
            }
        }
        catch { /* pakai default */ }

        AgentLog.Write("mcp.initialize", clientVer);
        return new
        {
            protocolVersion = clientVer,
            capabilities = new { tools = new { } },
            serverInfo = new { name = "flit", version = ServerVersion },
        };
    }

    private static object Tool(string name, string description, object properties, params string[] required) => new
    {
        name,
        description,
        inputSchema = new { type = "object", properties, required },
    };

    private static object ToolsList() => new
    {
        tools = new object[]
        {
            Tool("flit_list_tabs",
                "Daftar semua tab Flit: index, judul, URL, status loading/tidur, mana yang aktif.",
                new { }),
            Tool("flit_new_tab",
                "Buka tab baru. Default: tab latar (tidak aktif).",
                new
                {
                    url = new { type = "string", description = "URL awal (opsional; boleh kata kunci pencarian)" },
                    activate = new { type = "boolean", description = "Jadikan tab aktif? Default false." },
                }),
            Tool("flit_close_tab", "Tutup tab berdasar index.", new
            {
                index = new { type = "integer", description = "Index tab (dari flit_list_tabs)" },
            }, "index"),
            Tool("flit_activate_tab", "Jadikan tab tertentu tab aktif (tampil di depan).", new
            {
                index = new { type = "integer" },
            }, "index"),
            Tool("flit_navigate",
                "Navigasikan tab ke URL (atau kata kunci → pencarian DuckDuckGo). Menunggu halaman selesai dimuat (maks 30 dtk). Tunduk pada izin domain.",
                new
                {
                    url = new { type = "string" },
                    tabIndex = new { type = "integer", description = "Default: tab aktif" },
                }, "url"),
            Tool("flit_get_page_text",
                "Ambil teks halaman (innerText) — cara paling hemat untuk membaca konten.",
                new
                {
                    tabIndex = new { type = "integer" },
                    maxChars = new { type = "integer", description = "Batas panjang teks. Default 200000." },
                }),
            Tool("flit_snapshot",
                "Snapshot struktur aksesibilitas (AX tree) halaman via CDP — pohon role/name/value untuk agent. Paling informatif sebelum berinteraksi.",
                new
                {
                    tabIndex = new { type = "integer" },
                    maxNodes = new { type = "integer", description = "Batas jumlah node. Default 400, maks 2000." },
                }),
            Tool("flit_screenshot",
                "Tangkap PNG halaman (CDP Page.captureScreenshot) → disimpan ke file, hasilnya path + ukuran.",
                new
                {
                    tabIndex = new { type = "integer" },
                    path = new { type = "string", description = "Path PNG tujuan (opsional; default folder screenshots Flit)" },
                }),
            Tool("flit_find",
                "Cari teks di halaman; laporkan jumlah kemunculan + sorot kemunculan pertama.",
                new
                {
                    query = new { type = "string" },
                    tabIndex = new { type = "integer" },
                }, "query"),
            Tool("flit_evaluate_js",
                "Jalankan JavaScript di halaman dan ambil hasilnya. NONAKTIF secara default: butuh agent.allowEvaluate=true di config.json atau --allow-evaluate. Tunduk pada izin domain.",
                new
                {
                    script = new { type = "string" },
                    tabIndex = new { type = "integer" },
                }, "script"),
            Tool("flit_status",
                "Status Flit: versi, jumlah tab, ringkasan RAM, mode headless, izin agent.", new { }),
        },
    };

    private async Task<object> ToolsCallAsync(JsonElement req)
    {
        var p = req.GetProperty("params");
        var name = p.GetProperty("name").GetString() ?? "";
        var args = p.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object
            ? a.Clone() : JsonSerializer.SerializeToElement(new { });

        AgentLog.Write("mcp.tool_call", new { tool = name, args = args.ToString() });
        try
        {
            var result = await OnUiAsync(() => _form.McpCallAsync(name, args));
            AgentLog.Write("mcp.tool_ok", new { tool = name });
            return new
            {
                content = new object[] { new { type = "text", text = result } },
                isError = false,
            };
        }
        catch (McpToolError ex)
        {
            AgentLog.Write("mcp.tool_failed", new { tool = name, error = ex.Message });
            return new
            {
                content = new object[] { new { type = "text", text = ex.Message } },
                isError = true,
            };
        }
    }

    private Task<T> OnUiAsync<T>(Func<Task<T>> work)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _form.BeginInvoke(new Action(async () =>
        {
            try { tcs.SetResult(await work()); }
            catch (Exception ex) { tcs.SetException(ex); }
        }));
        return tcs.Task;
    }

    private void WriteJson(Stream stdout, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        lock (_writeLock)
        {
            stdout.Write(bytes);
            stdout.WriteByte((byte)'\n');
            stdout.Flush();
        }
    }
}
