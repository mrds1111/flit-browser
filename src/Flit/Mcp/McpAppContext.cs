using Flit.Mcp;

namespace Flit.Mcp;

/// <summary>
/// ApplicationContext untuk mode MCP: form dibuat tapi TIDAK pernah ditampilkan (headless).
/// Server stdio dibaca di thread latar; stdin EOF = klien pamit → keluar bersih.
/// </summary>
public class McpAppContext : ApplicationContext
{
    public int ExitCode { get; private set; }

    public McpAppContext(MainForm form, AgentConfig cfg)
    {
        var server = new McpServer(form, cfg);
        var initDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Inisialisasi (buka tab awal dsb.) HARUS tuntas sebelum tool dieksekusi —
        // kalau tidak, request awal klien mengenal bet tab kosong/_active=-1.
        form.BeginInvoke(new Action(async () =>
        {
            try
            {
                await form.InitializeAsync();
                initDone.SetResult();
            }
            catch (Exception ex)
            {
                initDone.SetException(ex);
            }
        }));

        _ = Task.Run(async () =>
        {
            try
            {
                await initDone.Task;                 // tunggu browser siap
                await server.RunAsync();             // blok sampai stdin EOF
                ExitCode = 0;
            }
            catch (Exception ex)
            {
                AgentLog.Write("mcp.fatal", ex.Message);
                ExitCode = 1;
            }
            finally
            {
                form.BeginInvoke(new Action(ExitThread));
            }
        });
    }
}
