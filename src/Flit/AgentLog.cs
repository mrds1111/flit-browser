using System.Text.Json;

namespace Flit;

/// <summary>
/// Log aktivitas agent (M2) — JSONL satu file per hari di %LOCALAPPDATA%\Flit\logs\.
/// Semua aksi lewat MCP dicatat di sini: siapa (tool) ngapain (detail), kapan (ts).
/// Tidak pernah melempar exception — logging tidak boleh bikin crash.
/// </summary>
public static class AgentLog
{
    private static readonly object Lock = new();

    public static string LogDir => Path.Combine(AgentConfig.ConfigDir, "logs");

    public static void Write(string action, object? detail)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var path = Path.Combine(LogDir, $"agent-{DateTime.Now:yyyyMMdd}.jsonl");
            var line = JsonSerializer.Serialize(new
            {
                ts = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss.fff"),
                action,
                detail,
            });
            lock (Lock) File.AppendAllText(path, line + Environment.NewLine);
        }
        catch { /* diamkan */ }
    }
}
