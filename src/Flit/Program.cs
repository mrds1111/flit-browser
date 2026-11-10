namespace Flit;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetColorMode(SystemColorMode.Dark); // dark mode bawaan .NET 10 untuk dialog standar
        Application.Run(new MainForm(args));
    }
}
