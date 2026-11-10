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

    // Halaman tab baru — inline, gelap, tanpa akses disk
    private const string NewTabHtml =
        """<!doctype html><html><head><meta charset="utf-8"><title>Tab Baru</title><style>body{background:#202124;color:#e8eaed;font-family:'Segoe UI',sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;margin:0}main{text-align:center}h1{font-weight:300;font-size:42px;letter-spacing:8px;margin-bottom:4px}p{color:#9aa0a6;margin:6px 0}a{color:#8ab4f8;text-decoration:none;margin:0 10px;font-size:15px}a:hover{text-decoration:underline}</style></head><body><main><h1>FLIT</h1><p><em>One browser, both of us.</em></p><p style="margin-top:28px"><a href="https://duckduckgo.com">DuckDuckGo</a><a href="https://github.com">GitHub</a><a href="https://id.wikipedia.org">Wikipedia</a><a href="https://news.ycombinator.com">Hacker News</a></p></main></body></html>""";

    private readonly List<FlitTab> _tabs = new();
    private readonly TabStrip _tabStrip;
    private readonly Panel _host = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 33, 36) };
    private readonly TextBox _omnibox;
    private readonly Label _status;
    private readonly Label _ram;
    private readonly System.Windows.Forms.Timer _ramTimer;
    private readonly System.Windows.Forms.Timer _sleepSweep;
    private readonly List<string> _initialUrls;
    private readonly int _sleepAfterSec; // default 60 dtk; override via --sleep-after=N (benchmark)
    private readonly string _userDataDir;
    private CoreWebView2Environment? _env;
    private int _active = -1;

    public MainForm(string[] args)
    {
        _sleepAfterSec = 60;
        _userDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flit", "M1");

        var urls = new List<string>();
        foreach (var a in args)
        {
            if (a.StartsWith("--sleep-after=", StringComparison.Ordinal) &&
                int.TryParse(a["--sleep-after=".Length..], out var sec))
            {
                _sleepAfterSec = Math.Max(3, sec);
            }
            else if (!a.StartsWith("--", StringComparison.Ordinal))
            {
                urls.Add(NormalizeUrl(a));
            }
        }
        _initialUrls = urls;

        // Chrome Flit digambar sendiri — dark mode = pilih warna gelap, pasti 100% gelap
        Color bg = Color.FromArgb(32, 33, 36);
        Color chromeBg = Color.FromArgb(43, 43, 43);
        Color fg = Color.FromArgb(232, 234, 237);
        Color dim = Color.FromArgb(154, 160, 166);

        Text = "Flit — M1";
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

        // Baris chrome: [←] [→] [⟳] [omnibox] [Go]
        var navRow = new Panel { Dock = DockStyle.Fill, BackColor = chromeBg, Padding = new Padding(6, 7, 6, 7) };
        var nav = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = Padding.Empty, BackColor = chromeBg };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nav.Controls.Add(NavButton("←", () => { var w = Current?.Web; if (w is { CanGoBack: true }) w.GoBack(); }), 0, 0);
        nav.Controls.Add(NavButton("→", () => { var w = Current?.Web; if (w is { CanGoForward: true }) w.GoForward(); }), 1, 0);
        nav.Controls.Add(NavButton("⟳", () => { if (Current?.Web?.CoreWebView2 is not null) Current!.Web!.Reload(); }), 2, 0);
        nav.Controls.Add(_omnibox, 3, 0);
        nav.Controls.Add(NavButton("Go", Navigate), 4, 0);
        navRow.Controls.Add(nav);

        _tabStrip = new TabStrip(_tabs);
        _tabStrip.TabSelected += i => ActivateTab(i);
        _tabStrip.TabClosed += CloseTab;
        _tabStrip.NewTabRequested += () => _ = OpenTabAsync(null);

        var chrome = new Panel { Dock = DockStyle.Top, Height = 82, BackColor = chromeBg };
        chrome.Controls.Add(navRow);   // Dock=Fill (di bawah tab strip)
        chrome.Controls.Add(_tabStrip); // Dock=Top (baris atas)

        // Urutan penambahan menentukan docking: Fill dulu, lalu Top, lalu Bottom
        Controls.Add(_host);
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

        // Sapuan tidur: tab latar yang sudah selesai load > N detik → TrySuspend
        _sleepSweep = new System.Windows.Forms.Timer { Interval = 5000 };
        _sleepSweep.Tick += (s, e) => SweepSleep();
        _sleepSweep.Start();
    }

    private FlitTab? Current => (_active >= 0 && _active < _tabs.Count) ? _tabs[_active] : null;

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

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.T: _ = OpenTabAsync(null); return true;
            case Keys.Control | Keys.W: CloseTab(_active); return true;
            case Keys.Control | Keys.L: _omnibox.Focus(); _omnibox.SelectAll(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static string NormalizeUrl(string input)
    {
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }
        return "https://" + input;
    }

    private static bool LooksLikeUrl(string s)
    {
        if (s.Contains(' ')) return false;
        if (s.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        int dot = s.IndexOf('.');
        return dot > 0 && dot < s.Length - 1 && !s.EndsWith(".", StringComparison.Ordinal);
    }

    private void Navigate()
    {
        var tab = Current;
        if (tab?.Web?.CoreWebView2 is null) return;
        var input = _omnibox.Text.Trim();
        if (input.Length == 0) return;

        string url = input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                     input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? input
            : LooksLikeUrl(input)
                ? NormalizeUrl(input)
                : SearchUrl + Uri.EscapeDataString(input);

        tab.Url = url;
        tab.Web.CoreWebView2.Navigate(url);
    }

    private async Task OpenTabAsync(string? url, bool activate = true)
    {
        var tab = new FlitTab { Url = url ?? string.Empty };
        _tabs.Add(tab);
        await EnsureWebAsync(tab);

        if (activate) ActivateTab(_tabs.Count - 1);
        else _tabStrip.Invalidate();
    }

    private async Task EnsureWebAsync(FlitTab tab)
    {
        if (tab.Web is not null) return;

        // Environment dibuat sekali & dipakai bersama → browser proses juga satu (hemat RAM)
        _env ??= await CoreWebView2Environment.CreateAsync(userDataFolder: _userDataDir);

        var w = new WebView2
        {
            Dock = DockStyle.Fill,
            Visible = false,
            DefaultBackgroundColor = Color.FromArgb(32, 33, 36),
            Tag = tab,
        };
        _host.Controls.Add(w);
        tab.Web = w;
        await w.EnsureCoreWebView2Async(_env);

        w.CoreWebView2.DocumentTitleChanged += (s, _) =>
        {
            tab.Title = string.IsNullOrEmpty(w.CoreWebView2.DocumentTitle) ? "Tab Baru" : w.CoreWebView2.DocumentTitle;
            if (ReferenceEquals(Current, tab)) Text = tab.Title + " — Flit";
            _tabStrip.Invalidate();
        };
        w.CoreWebView2.NavigationStarting += (s, _) =>
        {
            tab.IsLoading = true;
            _tabStrip.Invalidate();
        };
        w.CoreWebView2.NavigationCompleted += (s, _) =>
        {
            tab.IsLoading = false;
            // Tab latar mulai hitung mundur tidur setelah selesai memuat
            tab.BackgroundReadyAt = ReferenceEquals(Current, tab) ? DateTime.MaxValue : DateTime.Now;
            if (ReferenceEquals(Current, tab))
            {
                _status.Text = "Selesai: " + w.CoreWebView2.Source;
                SyncOmnibox();
            }
            _tabStrip.Invalidate();
        };

        tab.IsLoading = true;
        if (string.IsNullOrEmpty(tab.Url)) w.CoreWebView2.NavigateToString(NewTabHtml);
        else w.CoreWebView2.Navigate(tab.Url);
    }

    private void ActivateTab(int idx)
    {
        if (idx < 0 || idx >= _tabs.Count) return;

        if (Current?.Web is { } old)
        {
            old.Visible = false;
            if (!Current!.IsSleeping && !Current.IsLoading) Current.BackgroundReadyAt = DateTime.Now;
        }

        _active = idx;
        _tabStrip.SetActive(idx);

        var tab = _tabs[idx];
        if (tab.Web is { } w)
        {
            w.Visible = true;  // controller.IsVisible=true → auto-resume bila tertidur
            w.BringToFront();
        }
        tab.IsSleeping = false;
        tab.BackgroundReadyAt = DateTime.MaxValue;

        SyncOmnibox();
        Text = tab.Title + " — Flit";
        _omnibox.Focus();
    }

    private void SyncOmnibox()
    {
        var tab = Current;
        if (tab is null) return;
        _omnibox.Text = tab.Web?.CoreWebView2?.Source is { Length: > 0 } src ? src : tab.Url;
    }

    private void CloseTab(int idx)
    {
        if (idx < 0 || idx >= _tabs.Count) return;

        var tab = _tabs[idx];
        if (tab.Web is not null)
        {
            _host.Controls.Remove(tab.Web);
            tab.Web.Dispose();   // renderer proses milik tab ini ikut dibuang
            tab.Web = null;
        }
        _tabs.RemoveAt(idx);

        if (_tabs.Count == 0)
        {
            _active = -1;
            _ = OpenTabAsync(null);
            return;
        }
        if (idx == _active) ActivateTab(Math.Min(idx, _tabs.Count - 1));
        else
        {
            if (idx < _active) _active--;
            _tabStrip.SetActive(_active);
            _tabStrip.Invalidate();
        }
    }

    // M1 inti: tab latar yang selesai memuat > N detik → tidur (TrySuspend)
    private void SweepSleep()
    {
        var now = DateTime.Now;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var tab = _tabs[i];
            if (i == _active || tab.IsSleeping || tab.Web is null || tab.IsLoading) continue;
            if (tab.BackgroundReadyAt == DateTime.MaxValue) continue;
            if ((now - tab.BackgroundReadyAt).TotalSeconds >= _sleepAfterSec)
                _ = SleepTabAsync(tab);
        }
    }

    private async Task SleepTabAsync(FlitTab tab)
    {
        if (tab.Web?.CoreWebView2 is null || tab.IsSleeping) return;
        var ok = await tab.Web.CoreWebView2.TrySuspendAsync();
        if (ok)
        {
            tab.IsSleeping = true;
            _tabStrip.Invalidate();
        }
        // Gagal (mis. tab sedang main audio) → dicoba lagi sweep berikutnya
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
                if (cmd.Contains(@"\Flit\", StringComparison.OrdinalIgnoreCase))
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

        try
        {
            if (_initialUrls.Count == 0)
            {
                await OpenTabAsync(null);
            }
            else
            {
                foreach (var url in _initialUrls)
                {
                    await OpenTabAsync(url, activate: _tabs.Count == 0);
                }
            }

            // Bukti CDP: tanya engine lewat DevTools Protocol (dibroker internal,
            // tanpa remote debugging port — sesuai spesifikasi FULL-FINAL.md §2)
            if (Current?.Web?.CoreWebView2 is { } core)
            {
                var json = await core.CallDevToolsProtocolMethodAsync("Browser.getVersion", "{}");
                using var doc = JsonDocument.Parse(json);
                var product = doc.RootElement.GetProperty("product").GetString();
                _status.Text = $"CDP OK: {product}";
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Gagal init WebView2: " + ex.Message;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _ramTimer.Stop();
        _sleepSweep.Stop();
        base.OnFormClosed(e);
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
