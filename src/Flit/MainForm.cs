using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Flit;

public class MainForm : Form
{
    // Search engine default — satu konstanta; jadi setting pengguna di M3
    private const string SearchUrl = "https://duckduckgo.com/?q=";

    private readonly TextBox _omnibox;
    private readonly Label _status;
    private readonly Label _ram;
    private readonly WebView2 _webView;
    private readonly Panel _sleeper;
    private readonly System.Windows.Forms.Timer _ramTimer;
    private bool _suspended;

    public MainForm()
    {
        // Chrome Flit digambar sendiri — dark mode = pilih warna gelap, pasti 100% gelap
        Color bg = Color.FromArgb(32, 33, 36);        // latar utama
        Color chromeBg = Color.FromArgb(43, 43, 43);  // tab bar / omnibox
        Color fg = Color.FromArgb(232, 234, 237);     // teks terang
        Color dim = Color.FromArgb(154, 160, 166);    // teks redup

        Text = "Flit — M0.1";
        ClientSize = new Size(1200, 800);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = bg;
        Font = new Font("Segoe UI", 9.5f);

        _omnibox = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(60, 60, 64),
            ForeColor = fg,
        };

        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = dim,
            Text = "Menyiapkan engine...",
        };
        _ram = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = dim,
            Text = "RAM: ...",
        };

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = chromeBg };
        var bottomRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, BackColor = chromeBg };
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70f));
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30f));
        bottomRow.Controls.Add(_status, 0, 0);
        bottomRow.Controls.Add(_ram, 1, 0);
        bottom.Controls.Add(bottomRow);

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = bg,
        };

        // Overlay "tab tertidur" — menutupi webview saat disuspend (simulasi tab latar)
        _sleeper = new Panel { Dock = DockStyle.Fill, BackColor = bg, Visible = false };
        var sleepLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = dim,
            Font = new Font("Segoe UI", 14f),
            Text = "💤  Tab tertidur\n\nklik untuk membangunkan",
        };
        sleepLabel.Click += (s, e) => WakeTab();
        _sleeper.Click += (s, e) => WakeTab();
        _sleeper.Controls.Add(sleepLabel);

        var host = new Panel { Dock = DockStyle.Fill, BackColor = bg };
        host.Controls.Add(_webView);
        host.Controls.Add(_sleeper); // z-order: di atas webview, awalnya disembunyikan

        // Baris chrome: [←] [→] [⟳] [omnibox] [Go] [💤]
        var chrome = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = chromeBg, Padding = new Padding(6, 7, 6, 7) };
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, Margin = Padding.Empty, BackColor = chromeBg };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(NavButton("←", () => { if (_webView.CanGoBack) _webView.GoBack(); }), 0, 0);
        row.Controls.Add(NavButton("→", () => { if (_webView.CanGoForward) _webView.GoForward(); }), 1, 0);
        row.Controls.Add(NavButton("⟳", () => { if (_webView.CoreWebView2 is not null && !_suspended) _webView.Reload(); }), 2, 0);
        row.Controls.Add(_omnibox, 3, 0);
        row.Controls.Add(NavButton("Go", Navigate), 4, 0);
        row.Controls.Add(NavButton("💤", SleepTab), 5, 0);
        chrome.Controls.Add(row);

        // Urutan penambahan menentukan docking: Fill dulu, lalu Top, lalu Bottom
        Controls.Add(host);
        Controls.Add(chrome);
        Controls.Add(bottom);

        _omnibox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Navigate(); }
        };

        // Ticker RAM: shell + seluruh pohon WebView2 milik Flit, tiap 3 detik
        _ramTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _ramTimer.Tick += (s, e) => UpdateRam();
        _ramTimer.Start();
    }

    private Button NavButton(string label, Action onClick)
    {
        var b = new Button
        {
            Text = label,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(43, 43, 43),
            ForeColor = Color.FromArgb(232, 234, 237),
            Width = label == "Go" ? 42 : 32,
            Height = 30,
            Margin = new Padding(2, 0, 2, 0),
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, __) => onClick();
        return b;
    }

    private void Navigate()
    {
        if (_webView.CoreWebView2 is null) return;
        var input = _omnibox.Text.Trim();
        if (input.Length == 0) return;

        string url;
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = input;                                    // URL eksplisit
        }
        else if (LooksLikeUrl(input))
        {
            url = "https://" + input;                       // domain tanpa skema → https
        }
        else
        {
            url = SearchUrl + Uri.EscapeDataString(input);  // sisanya → pencarian
        }
        _webView.CoreWebView2.Navigate(url);
    }

    // Heuristik ala omnibox: "github.com" = URL, "github" = kata kunci pencarian
    private static bool LooksLikeUrl(string s)
    {
        if (s.Contains(' ')) return false;
        if (s.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        int dot = s.IndexOf('.');
        return dot > 0 && dot < s.Length - 1 && !s.EndsWith(".", StringComparison.Ordinal);
    }

    // M0.1: tidurkan tab — sembunyikan webview lalu TrySuspend (prasyarat resmi: IsVisible=false)
    private async void SleepTab()
    {
        if (_webView.CoreWebView2 is null || _suspended) return;
        _suspended = true;
        _sleeper.Visible = true;
        _sleeper.BringToFront();
        _webView.Visible = false; // controller.IsVisible=false — prasyarat suspend
        var ok = await _webView.CoreWebView2.TrySuspendAsync();
        _status.Text = ok
            ? "💤 Tab tertidur (TrySuspend OK) — pantau RAM di kanan bawah turun, klik layar untuk bangun"
            : "TrySuspend gagal — tab hanya disembunyikan (suspend umumnya menolak bila WebView terlihat)";
        UpdateRam();
    }

    private void WakeTab()
    {
        if (!_suspended) return;
        _webView.Visible = true;  // controller.IsVisible=true → WebView2 auto-resume
        _suspended = false;
        _sleeper.Visible = false;
        _status.Text = "Tab bangun kembali";
    }

    // RAM = shell + seluruh pohon msedgewebview2 milik Flit (di-filter user-data-folder)
    private void UpdateRam()
    {
        try
        {
            long ws = 0, pm = 0;
            using var me = Process.GetCurrentProcess();
            ws += me.WorkingSet64;
            pm += me.PrivateMemorySize64;

            foreach (var pid in GetFlitWebviewPids())
            {
                try
                {
                    using var p = Process.GetProcessById(pid);
                    ws += p.WorkingSet64;
                    pm += p.PrivateMemorySize64;
                }
                catch { /* proses sudah exit di antara query — abaikan */ }
            }

            double mbWs = ws / 1024.0 / 1024.0, mbPm = pm / 1024.0 / 1024.0;
            _ram.Text = $"RAM: {mbPm:F0} MB private • {mbWs:F0} MB WS";
            _ram.ForeColor = _suspended ? Color.FromArgb(126, 211, 127) : Color.FromArgb(154, 160, 166);
        }
        catch { /* ticker tidak boleh bikin crash */ }
    }

    private static IEnumerable<int> GetFlitWebviewPids()
    {
        var result = new List<int>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='msedgewebview2.exe'");
            foreach (ManagementObject o in searcher.Get())
            {
                var cmd = o["CommandLine"] as string ?? string.Empty;
                if (cmd.Contains(@"Flit\M0", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(Convert.ToInt32(o["ProcessId"]));
                }
            }
        }
        catch { /* WMI kadang sibuk — tampilkan angka parsial saja */ }
        return result;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _omnibox.Select();

        try
        {
            // Profil data terpisah — tidak menyentuh Edge/Chrome yang terpasang
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Flit", "M0");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await _webView.EnsureCoreWebView2Async(env);

            _webView.CoreWebView2.DocumentTitleChanged += (s, _) =>
                Text = string.IsNullOrEmpty(_webView.CoreWebView2.DocumentTitle)
                    ? "Flit — M0.1"
                    : _webView.CoreWebView2.DocumentTitle + " — Flit";

            _webView.CoreWebView2.NavigationCompleted += (s, _) =>
            {
                if (!_suspended) _status.Text = $"Selesai: {_webView.CoreWebView2.Source}";
            };

            _omnibox.Text = "https://example.com";
            _webView.CoreWebView2.Navigate("https://example.com");

            // Bukti CDP M0: tanya engine langsung lewat DevTools Protocol (dibroker internal,
            // tanpa remote debugging port — sesuai spesifikasi FULL-FINAL.md §2)
            var json = await _webView.CoreWebView2.CallDevToolsProtocolMethodAsync("Browser.getVersion", "{}");
            using var doc = JsonDocument.Parse(json);
            var product = doc.RootElement.GetProperty("product").GetString();
            var ua = doc.RootElement.GetProperty("userAgent").GetString();
            _status.Text = $"CDP OK: {product}";
            Console.WriteLine($"[Flit M0] CDP Browser.getVersion -> product={product}");

            UpdateRam();
        }
        catch (Exception ex)
        {
            _status.Text = "Gagal init WebView2: " + ex.Message;
        }
    }

    // Title bar gelap di Windows 10/11
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int sizeBytes);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1; // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
        DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
    }
}
