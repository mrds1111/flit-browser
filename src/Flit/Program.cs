using Flit;

namespace Flit;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetColorMode(SystemColorMode.Dark); // dark mode bawaan .NET 10 untuk dialog standar

        if (args.Any(a => a == "--mcp"))
        {
            // Mode agent (M2): headless — jendela tidak pernah ditampilkan,
            // protokol MCP bicara lewat stdin/stdout. Keluar saat klien menutup stdin.
            var cfg = AgentConfig.Load();
            cfg.ApplyArgs(args);
            var form = new MainForm(args);
            var _ = form.Handle; // paksa handle dibuat tanpa Show() — syarat WebView2 anak-anaknya
            Application.Run(new Mcp.McpAppContext(form, cfg));
            return 0;
        }

        Application.Run(new MainForm(args));
        return 0;
    }
}
