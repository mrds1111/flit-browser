using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;
using Flit.Adblock;
using Flit.Mcp;
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

    // ==== M3: find bar + bookmark star ====
    private readonly Panel _findBar = new() { Dock = DockStyle.Bottom, Height = 36, Visible = false, BackColor = Color.FromArgb(43, 43, 43), Padding = new Padding(6, 5, 6, 5) };
    private readonly TextBox _findBox = new() { Width = 240, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.FromArgb(60, 60, 64), ForeColor = Color.FromArgb(232, 234, 237) };
    private readonly Label _findCount = new() { Width = 90, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(154, 160, 166), Text = "" };
    private Button? _star;
    private string _lastFindQuery = "";

    // ==== M2: mode agent ====
    public AgentConfig AgentCfg { get; }
    /// <summary>True pada `--mcp`: jendela tidak pernah ditampilkan (headless).</summary>
    public bool Headless { get; }

    public MainForm(string[] args)
    {
        AgentCfg = AgentConfig.Load();
        AgentCfg.ApplyArgs(args);
        Headless = args.Any(a => a == "--mcp");

        _sleepAfterSec = 60;
        _userDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flit", "Default");

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
        _star = NavButton("☆", () => _ = ToggleBookmarkAsync());
        _star.Width = 30;
        nav.Controls.Add(_star, 5, 0);
        nav.ColumnCount = 6;
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
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
        Controls.Add(BuildMenu(chromeBg, fg, dim));
        Controls.Add(_findBar);
        Controls.Add(bottom);

        BuildFindBar();

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

        // M3: adblock bawaan (list unduh/cache di background; handler pasang per-webview)
        AdblockStore.Initialize(AgentCfg, OnAdblockEngineReady);
    }

    private async Task<CoreWebView2Environment> CreateEnvAsync()
    {
        // S1 stealth dasar (M2): buang penanda automation bawaan Chromium
        // (--disable-blink-features=AutomationControlled → navigator.webdriver = false)
        var opts = new CoreWebView2EnvironmentOptions
        {
            AdditionalBrowserArguments = "--disable-blink-features=AutomationControlled",
        };
        if (!string.IsNullOrEmpty(AgentCfg.Language)) opts.Language = AgentCfg.Language;   // S1: --lang
        return await CoreWebView2Environment.CreateAsync(userDataFolder: _userDataDir, options: opts);
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
            case Keys.Control | Keys.F: ShowFindBar(); return true;
            case Keys.Control | Keys.D: _ = ToggleBookmarkAsync(); return true;
            case Keys.Control | Keys.H: ShowHistory(); return true;
            case Keys.Control | Keys.S: _ = SavePageAsync(mhtml: true); return true;
            case Keys.Escape when _findBar.Visible: HideFindBar(); return true;
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

    /// <summary>Input omnibox/agent → URL final: skema eksplisit langsung; mirip-URL kasih https; sisanya pencarian.</summary>
    private static string ResolveInput(string input)
    {
        var t = input.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return t;
        }
        return LooksLikeUrl(t) ? NormalizeUrl(t) : SearchUrl + Uri.EscapeDataString(t);
    }

    private void Navigate()
    {
        var tab = Current;
        if (tab?.Web?.CoreWebView2 is null) return;
        var input = _omnibox.Text.Trim();
        if (input.Length == 0) return;

        string url = ResolveInput(input);
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
        _env ??= await CreateEnvAsync();

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

        // S1: UA per-profil (override engine, diterapkan pada tiap WebView2 baru)
        if (!string.IsNullOrEmpty(AgentCfg.UserAgent))
            w.CoreWebView2.Settings.UserAgent = AgentCfg.UserAgent;

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
            // M3: catat history (hanya navigasi tingkat atas yang tiba di sini)
            var src = w.CoreWebView2.Source;
            if (src is { Length: > 0 } && !src.StartsWith("about:", StringComparison.Ordinal))
                HistoryStore.Append(src, tab.Title);
            _tabStrip.Invalidate();
        };

        // M3: unduhan → folder Downloads\Flit, progress di status bar
        w.CoreWebView2.DownloadStarting += (s, e) =>
        {
            try
            {
                e.Handled = true;
                var op = e.DownloadOperation;
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Flit");
                Directory.CreateDirectory(dir);
                var name = "download-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try
                {
                    var fromUrl = new Uri(op.Uri).AbsolutePath.Split('/').LastOrDefault(x => x.Length > 0);
                    if (!string.IsNullOrEmpty(fromUrl)) name = Uri.UnescapeDataString(fromUrl);
                }
                catch { }
                e.ResultFilePath = UniquePath(Path.Combine(dir, SanitizeFileName(name)));
                op.StateChanged += (_, _) =>
                {
                    if (op.State == CoreWebView2DownloadState.Completed)
                        BeginInvoke(new Action(() => _status.Text = "Unduhan selesai: " + op.ResultFilePath));
                    else if (op.State == CoreWebView2DownloadState.Interrupted)
                        BeginInvoke(new Action(() => _status.Text = "Unduhan terputus: " + op.ResultFilePath));
                };
                AgentLog.Write("download", new { url = op.Uri, path = op.ResultFilePath });
                _status.Text = "Mengunduh: " + op.ResultFilePath;
            }
            catch { /* unduhan gagal atur → biarkan default behavior */ }
        };

        // M3: adblock lapisan 1 — network blocking (handler HARUS cepat: O(1) per domain)
        w.CoreWebView2.WebResourceRequested += (s, e) =>
        {
            var engine = AdblockStore.Current;
            if (engine is null) return;
            try
            {
                string? refHost = null;
                if (e.Request.Headers.Contains("Referer"))
                {
                    var r = e.Request.Headers.GetHeader("Referer");
                    if (Uri.TryCreate(r, UriKind.Absolute, out var ru))
                        refHost = ru.Host;
                }
                if (!engine.ShouldBlock(e.Request.Uri, MapResourceType(e.ResourceContext), refHost))
                    return;
                AdblockStore.BlockedCount++;
                e.Response = _env!.CreateWebResourceResponse(null, 200, "OK", "");
            }
            catch { /* jangan pernah ganggu load halaman */ }
        };
        // Semua konteks resource (service worker tak tercakup API — celah terdokumentasi)
        w.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

        // M3: adblock lapisan 2 — cosmetic (## element hiding). AddScriptToExecuteOnDocumentCreatedAsync
        // terbukti gagal senyap utk script besar di uji M3 → pakai DOMContentLoaded + ExecuteScriptAsync,
        // mekanisme yang sama dengan MCP evaluate (terbukti jalan headless).
        w.CoreWebView2.DOMContentLoaded += (s, e) =>
        {
            try
            {
                foreach (var js in AdblockStore.CosmeticScriptsCache)
                    _ = w.CoreWebView2.ExecuteScriptAsync(js);
            }
            catch (Exception ex) { AgentLog.Write("adblock.cosmetic_fail", ex.Message); }
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
        UpdateStar();
    }

    private void UpdateStar()
    {
        if (_star is null) return;
        var url = Current?.Web?.CoreWebView2?.Source;
        _star.Text = url is { Length: > 10 } && BookmarksStore.IsBookmarked(url) ? "★" : "☆";
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
            _ram.Text = $"RAM: {mbPm:F0} MB private • {mbWs:F0} MB WS" +
                        (AdblockStore.Current is not null ? $" • blokir {AdblockStore.BlockedCount}" : "");
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

    /// <summary>
    /// Buka tab awal + bukti CDP. Dipanggil dari OnLoad (jendela normal)
    /// maupun dari McpAppContext (headless — OnLoad tidak pernah fire tanpa Show()).
    /// </summary>
    public async Task InitializeAsync()
    {
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
                AgentLog.Write("startup", new { headless = Headless, cdp = product });
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Gagal init WebView2: " + ex.Message;
            AgentLog.Write("startup.failed", ex.Message);
        }
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await InitializeAsync();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _ramTimer.Stop();
        _sleepSweep.Stop();
        base.OnFormClosed(e);
    }

    // ==================== M3: fitur harian ====================

    private MenuStrip BuildMenu(Color chromeBg, Color fg, Color dim)
    {
        var menu = new MenuStrip
        {
            BackColor = chromeBg,
            ForeColor = fg,
            Renderer = new DarkMenuRenderer(),
            Padding = new Padding(4, 2, 4, 2),
        };

        var mi = new ToolStripMenuItem("Menu") { ForeColor = fg };
        mi.DropDownItems.Add(Item("Tab baru    Ctrl+T", (s, e) => _ = OpenTabAsync(null)));
        mi.DropDownItems.Add(Item("Cari di halaman    Ctrl+F", (s, e) => ShowFindBar()));
        mi.DropDownItems.Add(Item("Riwayat    Ctrl+H", (s, e) => ShowHistory()));
        mi.DropDownItems.Add(new ToolStripSeparator());
        mi.DropDownItems.Add(Item("Simpan halaman (MHTML)    Ctrl+S", (s, e) => _ = SavePageAsync(true)));
        mi.DropDownItems.Add(Item("Simpan PDF", (s, e) => _ = SavePageAsync(false)));
        mi.DropDownItems.Add(new ToolStripSeparator());
        ToolStripMenuItem adItem = null!;
        adItem = Item("Adblock: " + (AdblockStore.Current is null ? "NONAKTIF" : "aktif"), (s, e) =>
        {
            var turningOn = AdblockStore.Current is null;
            AdblockStore.SetEnabled(turningOn, AgentCfg, OnAdblockEngineReady);
            adItem.Text = "Adblock: " + (turningOn ? "aktif" : "NONAKTIF");
            _status.Text = turningOn ? "Adblock dinyalakan (memuat daftar...)" : "Adblock dimatikan";
        });
        mi.DropDownItems.Add(adItem);
        mi.DropDownItems.Add(Item("Tentang Flit", (s, e) =>
            MessageBox.Show("Flit Browser 0.3 (M3)\nOne browser, both of us.\n\nWebView2 (Chromium Evergreen) • MPL-2.0\nAdblock: EasyList/EasyPrivacy/ABPindo (CC BY-SA 3.0)",
                "Tentang Flit", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        var bm = new ToolStripMenuItem("Bookmark") { ForeColor = fg };
        bm.DropDownOpening += (s, e) => RebuildBookmarkMenu(bm);

        menu.Items.Add(mi);
        menu.Items.Add(bm);
        return menu;
    }

    private static ToolStripMenuItem Item(string text, EventHandler onClick) =>
        new(text) { ForeColor = Color.FromArgb(232, 234, 237) };

    private void RebuildBookmarkMenu(ToolStripDropDownItem bm)
    {
        bm.DropDownItems.Clear();
        var cur = Current?.Web?.CoreWebView2?.Source;
        if (cur is { Length: > 10 } && !cur.StartsWith("about:", StringComparison.Ordinal))
        {
            var marked = BookmarksStore.IsBookmarked(cur);
            bm.DropDownItems.Add(Item(marked ? "★ Hapus bookmark halaman ini" : "☆ Bookmark halaman ini    Ctrl+D",
                (s, e) => _ = ToggleBookmarkAsync()));
        }
        var all = BookmarksStore.All();
        if (all.Count > 0) bm.DropDownItems.Add(new ToolStripSeparator());
        foreach (var b in all.AsEnumerable().Reverse())
        {
            var url = b.Url;
            var title = b.Title.Length > 60 ? b.Title[..60] + "…" : b.Title;
            var it = Item("•  " + title, (s, e) => _ = OpenTabAsync(url));
            it.ToolTipText = url;
            bm.DropDownItems.Add(it);
        }
        if (all.Count == 0)
            bm.DropDownItems.Add(Item("(kosong — tekan Ctrl+D untuk mem-bookmark)", (s, e) => { }));
    }

    // ---- find in page ----

    private void BuildFindBar()
    {
        var prev = NavButton("‹", () => _ = RunFindAsync(false, true));
        var next = NavButton("›", () => _ = RunFindAsync(false, false));
        var close = NavButton("✕", HideFindBar);
        _findBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _ = RunFindAsync(_findBox.Text != _lastFindQuery, e.Shift);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                HideFindBar();
            }
        };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = _findBar.BackColor,
            WrapContents = false,
        };
        flow.Controls.Add(_findBox);
        flow.Controls.Add(_findCount);
        flow.Controls.Add(prev);
        flow.Controls.Add(next);
        flow.Controls.Add(close);
        _findBar.Controls.Add(flow);
    }

    private void ShowFindBar()
    {
        _findBar.Visible = true;
        _findBox.Focus();
        _findBox.SelectAll();
    }

    private void HideFindBar()
    {
        _findBar.Visible = false;
        _lastFindQuery = "";
        if (Current?.Web?.CoreWebView2 is { } core)
            _ = core.ExecuteScriptAsync("try{window.getSelection().removeAllRanges()}catch(e){}");
        _omnibox.Focus();
    }

    /// <summary>fresh = hitung jumlah + sorot pertama; else maju/mundur ke kemunculan berikutnya.</summary>
    private async Task RunFindAsync(bool fresh, bool back)
    {
        try
        {
            var core = Current?.Web?.CoreWebView2;
            if (core is null || !_findBar.Visible) return;
            var q = _findBox.Text;
            if (q.Length == 0) { _findCount.Text = ""; return; }

            if (fresh)
            {
                _lastFindQuery = q;
                var js = FindJs.Replace("%Q%", JsonSerializer.Serialize(q));
                var raw = await core.ExecuteScriptAsync(js);
                var json = JsonSerializer.Deserialize<string>(raw) ?? "{}";
                using var doc = JsonDocument.Parse(json);
                var count = doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : 0;
                _findCount.Text = count == 0 ? "tidak ada" : count + " hasil";
            }
            else
            {
                var js = FindNextJs.Replace("%Q%", JsonSerializer.Serialize(q))
                                   .Replace("%BACK%", back ? "true" : "false");
                var raw = await core.ExecuteScriptAsync(js);
                if (raw == "false") _findCount.Text = "tepi halaman";
            }
        }
        catch { /* find tidak boleh crash */ }
    }

    // ---- bookmark / history / simpan ----

    private Task ToggleBookmarkAsync()
    {
        try
        {
            var core = Current?.Web?.CoreWebView2;
            if (core is null) return Task.CompletedTask;
            var url = core.Source;
            if (string.IsNullOrEmpty(url) || url.StartsWith("about:", StringComparison.Ordinal))
                return Task.CompletedTask;
            var added = BookmarksStore.Toggle(url, Current!.Title);
            _status.Text = added ? "★ Dibookmark: " + url : "Bookmark dihapus";
            UpdateStar();
            AgentLog.Write("bookmark", new { url, added });
        }
        catch { }
        return Task.CompletedTask;
    }

    private void ShowHistory()
    {
        new HistoryForm(url => _ = OpenTabAsync(url)).Show(this);
    }

    private async Task SavePageAsync(bool mhtml)
    {
        var core = Current?.Web?.CoreWebView2;
        if (core is null) return;
        try
        {
            var title = Current?.Title ?? "halaman";
            if (mhtml)
            {
                _status.Text = "Menyimpan MHTML...";
                var raw = await core.CallDevToolsProtocolMethodAsync("Page.captureSnapshot", "{\"format\":\"mhtml\"}");
                using var doc = JsonDocument.Parse(raw);
                var data = doc.RootElement.GetProperty("data").GetString() ?? "";
                var path = SavePathFor(title, ".mhtml");
                await File.WriteAllTextAsync(path, data);
                _status.Text = "Tersimpan: " + path;
                AgentLog.Write("save.mhtml", new { url = core.Source, path });
            }
            else
            {
                _status.Text = "Mencetak PDF...";
                var raw = await core.CallDevToolsProtocolMethodAsync("Page.printToPDF", "{}");
                using var doc = JsonDocument.Parse(raw);
                var b64 = doc.RootElement.GetProperty("data").GetString() ?? "";
                var path = SavePathFor(title, ".pdf");
                await File.WriteAllBytesAsync(path, Convert.FromBase64String(b64));
                _status.Text = "Tersimpan: " + path;
                AgentLog.Write("save.pdf", new { url = core.Source, path });
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Gagal simpan: " + ex.Message;
        }
    }

    private static string SavePathFor(string title, string ext)
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Flit");
        Directory.CreateDirectory(dir);
        var baseName = SanitizeFileName(string.IsNullOrWhiteSpace(title) ? "halaman" : title);
        return UniquePath(Path.Combine(dir, baseName + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ext));
    }

    private static string SanitizeFileName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(Array.IndexOf(bad, ch) >= 0 ? '_' : ch);
        var s = sb.ToString().Trim();
        if (s.Length > 120) s = s[..120];
        return s.Length == 0 ? "file" : s;
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; i < 1000; i++)
        {
            var cand = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(cand)) return cand;
        }
        return path;
    }

    // ---- adblock support ----

    /// <summary>Engine daftar siap (belakangan, background) → pasang kosmetik ke tab yang sudah ada.</summary>
    private void OnAdblockEngineReady(FilterEngine engine)
    {
        if (IsDisposed) return;
        BeginInvoke(new Action(() =>
        {
            try
            {
                foreach (var t in _tabs)
                {
                    if (t.Web?.CoreWebView2 is not { } c) continue;
                    foreach (var js in AdblockStore.CosmeticScriptsCache)
                        _ = c.ExecuteScriptAsync(js); // dokumen saat ini; dokumen berikutnya lewat DOMContentLoaded
                }
                _status.Text = $"Adblock aktif: {engine.NetRules} aturan network • {engine.CosmeticRules} cosmetic";
            }
            catch { }
        }));
    }

    private static string MapResourceType(CoreWebView2WebResourceContext ctx) => ctx switch
    {
        CoreWebView2WebResourceContext.Script => "script",
        CoreWebView2WebResourceContext.Image => "image",
        CoreWebView2WebResourceContext.Stylesheet => "stylesheet",
        CoreWebView2WebResourceContext.Media => "media",
        CoreWebView2WebResourceContext.Font => "font",
        CoreWebView2WebResourceContext.XmlHttpRequest => "xmlhttprequest",
        CoreWebView2WebResourceContext.Fetch => "xmlhttprequest",
        CoreWebView2WebResourceContext.Ping => "ping",
        CoreWebView2WebResourceContext.Websocket => "websocket",
        CoreWebView2WebResourceContext.Document => "document",
        _ => "other",
    };

    // Renderer gelap untuk MenuStrip — tanpa panel putih
    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColors()) { }
    }

    private sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(60, 60, 64);
        public override Color MenuItemBorder => Color.FromArgb(60, 60, 64);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(60, 60, 64);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(60, 60, 64);
        public override Color ToolStripDropDownBackground => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientBegin => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(43, 43, 43);
        public override Color ImageMarginGradientEnd => Color.FromArgb(43, 43, 43);
        public override Color SeparatorDark => Color.FromArgb(70, 70, 74);
        public override Color SeparatorLight => Color.FromArgb(70, 70, 74);
    }

    // ==================== M2: Tools MCP ====================
    // Satpam ke McpServer: semua eksekusi tiba di UI thread dari sini, dan
    // SEMUA aksi agent tunduk pada izin domain (AgentCfg) + dicatat AgentLog.

    public async Task<string> McpCallAsync(string tool, JsonElement args)
    {
        return tool switch
        {
            "flit_list_tabs" => McpListTabs(),
            "flit_new_tab" => await McpNewTabAsync(args),
            "flit_close_tab" => McpCloseTool(args),
            "flit_activate_tab" => McpActivateTool(args),
            "flit_navigate" => await McpNavigateAsync(args),
            "flit_get_page_text" => await McpGetPageTextAsync(args),
            "flit_snapshot" => await McpSnapshotAsync(args),
            "flit_screenshot" => await McpScreenshotAsync(args),
            "flit_find" => await McpFindAsync(args),
            "flit_evaluate_js" => await McpEvaluateAsync(args),
            "flit_status" => McpStatus(),
            _ => throw new McpToolError($"Tool tidak dikenal: {tool}"),
        };
    }

    private static string? ArgStr(JsonElement a, string name) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static int ArgInt(JsonElement a, string name, int def) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : def;

    private static bool ArgBool(JsonElement a, string name, bool def) =>
        a.ValueKind == JsonValueKind.Object && a.TryGetProperty(name, out var v) &&
        (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : def;

    private FlitTab ResolveTab(JsonElement args)
    {
        // defensif: _active belum di-set tapi tab sudah ada → pakai tab pertama
        var def = _active >= 0 ? _active : (_tabs.Count > 0 ? 0 : -1);
        var idx = ArgInt(args, "tabIndex", def);
        if (idx < 0 || idx >= _tabs.Count)
            throw new McpToolError($"tabIndex {idx} tidak ada (0..{_tabs.Count - 1})");
        return _tabs[idx];
    }

    /// <summary>Bangunkan tab yang tidur (TrySuspend) sebelum disentuh agent/API.</summary>
    private void WakeTab(FlitTab tab)
    {
        if (!tab.IsSleeping || tab.Web is null) return;
        try { tab.Web.CoreWebView2?.Resume(); } catch { /* sudah bangun */ }
        tab.IsSleeping = false;
        if (!ReferenceEquals(Current, tab) && !tab.IsLoading) tab.BackgroundReadyAt = DateTime.Now;
        _tabStrip.Invalidate();
    }

    private async Task<bool> WaitForLoadAsync(FlitTab tab, int timeoutMs = 30000)
    {
        var sw = Stopwatch.StartNew();
        while (tab.IsLoading && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(100);
        return !tab.IsLoading;
    }

    private string McpListTabs()
    {
        var tabs = _tabs.Select((t, i) => new
        {
            index = i,
            title = t.Title,
            url = t.Web?.CoreWebView2?.Source ?? t.Url,
            isLoading = t.IsLoading,
            isSleeping = t.IsSleeping,
            isActive = i == _active,
        });
        return JsonSerializer.Serialize(new { tabs });
    }

    private async Task<string> McpNewTabAsync(JsonElement args)
    {
        var url = ArgStr(args, "url");
        var activate = ArgBool(args, "activate", false);
        if (url is not null)
        {
            var final = ResolveInput(url);
            if (!AgentCfg.IsDomainAllowed(final))
            {
                AgentLog.Write("agent.denied", new { action = "new_tab", url = final });
                throw new McpToolError($"Domain tidak diizinkan: {new Uri(final).Host}");
            }
        }
        await OpenTabAsync(url is null ? null : ResolveInput(url), activate);
        var idx = _tabs.Count - 1;
        if (url is not null) await WaitForLoadAsync(_tabs[idx]);
        AgentLog.Write("agent.new_tab", new { index = idx, url, activate });
        return JsonSerializer.Serialize(new { index = idx, activated = activate });
    }

    private string McpCloseTool(JsonElement args)
    {
        var idx = ArgInt(args, "index", -1);
        if (idx < 0 || idx >= _tabs.Count) throw new McpToolError($"index {idx} tidak ada");
        CloseTab(idx);
        AgentLog.Write("agent.close_tab", idx);
        return JsonSerializer.Serialize(new { closed = idx, tabs = _tabs.Count });
    }

    private string McpActivateTool(JsonElement args)
    {
        var idx = ArgInt(args, "index", -1);
        if (idx < 0 || idx >= _tabs.Count) throw new McpToolError($"index {idx} tidak ada");
        ActivateTab(idx);
        AgentLog.Write("agent.activate_tab", idx);
        return JsonSerializer.Serialize(new { active = idx });
    }

    private async Task<string> McpNavigateAsync(JsonElement args)
    {
        var tab = ResolveTab(args);
        var input = ArgStr(args, "url") ?? throw new McpToolError("url wajib diisi");
        var url = ResolveInput(input);
        if (!AgentCfg.IsDomainAllowed(url))
        {
            AgentLog.Write("agent.denied", new { action = "navigate", url });
            throw new McpToolError($"Domain tidak diizinkan: {new Uri(url).Host}");
        }
        WakeTab(tab);
        if (tab.Web?.CoreWebView2 is null) throw new McpToolError("Engine tab belum siap");
        tab.IsLoading = true; // set dulu → WaitForLoadAsync tak kalah rasa dengan NavigationStarting
        tab.Url = url;
        tab.Web.CoreWebView2.Navigate(url);
        var ok = await WaitForLoadAsync(tab);
        AgentLog.Write("agent.navigate", new { url, loaded = ok });
        return JsonSerializer.Serialize(new { index = _tabs.IndexOf(tab), url, loaded = ok });
    }

    private async Task<string> McpGetPageTextAsync(JsonElement args)
    {
        var tab = ResolveTab(args);
        WakeTab(tab);
        if (tab.Web?.CoreWebView2 is null) throw new McpToolError("Engine tab belum siap");
        var max = Math.Clamp(ArgInt(args, "maxChars", 200_000), 100, 2_000_000);
        var raw = await tab.Web.CoreWebView2.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
        var text = JsonSerializer.Deserialize<string>(raw) ?? "";
        AgentLog.Write("agent.read_text", new { url = tab.Web.CoreWebView2.Source, chars = text.Length });
        return JsonSerializer.Serialize(new
        {
            index = _tabs.IndexOf(tab),
            url = tab.Web.CoreWebView2.Source,
            text = text.Length > max ? text[..max] : text,
            truncated = text.Length > max,
        });
    }

    private static string AxField(JsonElement n, string key)
    {
        if (!n.TryGetProperty(key, out var o) || o.ValueKind != JsonValueKind.Object) return "";
        if (o.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String) return v.GetString() ?? "";
        return "";
    }

    private async Task<string> McpSnapshotAsync(JsonElement args)
    {
        var tab = ResolveTab(args);
        WakeTab(tab);
        if (tab.Web?.CoreWebView2 is null) throw new McpToolError("Engine tab belum siap");
        var maxNodes = Math.Clamp(ArgInt(args, "maxNodes", 400), 10, 2000);

        var raw = await tab.Web.CoreWebView2.CallDevToolsProtocolMethodAsync("Accessibility.getFullAXTree", "{}");
        using var doc = JsonDocument.Parse(raw);
        var nodes = new List<object>(Math.Min(maxNodes, 512));
        var truncated = false;
        foreach (var n in doc.RootElement.GetProperty("nodes").EnumerateArray())
        {
            if (n.TryGetProperty("ignored", out var ig) && ig.ValueKind == JsonValueKind.True) continue;
            if (nodes.Count >= maxNodes) { truncated = true; break; }
            var childIds = n.TryGetProperty("childIds", out var c)
                ? c.EnumerateArray().Select(x => x.GetInt64()).ToArray()
                : Array.Empty<long>();
            nodes.Add(new
            {
                id = n.TryGetProperty("nodeId", out var nid) ? nid.GetInt64() : 0L,
                role = AxField(n, "role"),
                name = AxField(n, "name"),
                value = AxField(n, "value"),
                childIds,
            });
        }
        var url = tab.Web.CoreWebView2.Source ?? tab.Url;
        AgentLog.Write("agent.snapshot", new { url, nodes = nodes.Count, truncated });
        return JsonSerializer.Serialize(new
        {
            index = _tabs.IndexOf(tab),
            url,
            nodes,
            truncated,
        });
    }

    private async Task<string> McpScreenshotAsync(JsonElement args)
    {
        var tab = ResolveTab(args);
        WakeTab(tab);
        if (tab.Web?.CoreWebView2 is null) throw new McpToolError("Engine tab belum siap");

        var dir = Path.Combine(AgentConfig.ConfigDir, "screenshots");
        Directory.CreateDirectory(dir);
        var path = ArgStr(args, "path");
        if (string.IsNullOrWhiteSpace(path))
            path = Path.Combine(dir, $"flit-{DateTime.Now:yyyyMMdd-HHmmss}-{_tabs.IndexOf(tab)}.png");

        // WebView2 tersembunyi berhenti mengomposisi → tampilkan sekejap, tangkap, pulihkan.
        // Di mode headless form induk tak pernah tampil, jadi tidak ada flash di layar.
        var wasVisible = tab.Web.Visible;
        tab.Web.Visible = true;
        await Task.Delay(250);

        // Form tersembunyi bisa membuat compositor tak mengirim frame → beberapa varian
        // parameter CDP dicoba berurutan, masing-masing dengan batas waktu. Tanpa ini
        // agent bisa menggantung selamanya menunggu respons.
        string data = "";
        foreach (var param in new[]
                 {
                     "{\"format\":\"png\",\"fromSurface\":false}",
                     "{\"format\":\"png\",\"captureBeyondViewport\":true}",
                     "{\"format\":\"png\"}",
                 })
        {
            var cdpTask = tab.Web.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", param);
            var done = await Task.WhenAny(cdpTask, Task.Delay(TimeSpan.FromSeconds(10)));
            if (done == cdpTask)
            {
                using var doc = JsonDocument.Parse(cdpTask.Result);
                data = doc.RootElement.GetProperty("data").GetString() ?? "";
                if (data.Length > 0) break;
            }
        }
        tab.Web.Visible = wasVisible;

        if (data.Length == 0)
            throw new McpToolError(
                "Screenshot gagal: engine tidak mengirim frame (host tersembunyi). Gunakan flit_snapshot / flit_get_page_text sebagai gantinya.");

        var bytes = Convert.FromBase64String(data);
        File.WriteAllBytes(path, bytes);
        AgentLog.Write("agent.screenshot", new { url = tab.Web.CoreWebView2.Source, path, bytes = bytes.Length });
        return JsonSerializer.Serialize(new { path, bytes = bytes.Length });
    }

    // Cari teks (case-insensitive) + sorot kemunculan pertama — dipakai UI find bar (M3) juga
    private const string FindJs = """
(function(){
  var q = %Q%;
  if(!q) return JSON.stringify({count:0});
  var count = 0, first = null;
  var root = document.body || document.documentElement;
  if(!root) return JSON.stringify({count:0});
  var w = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
  var n; var qq = q.toLowerCase();
  while((n = w.nextNode())){
    var t = n.textContent || ''; var low = t.toLowerCase(); var i = 0;
    while((i = low.indexOf(qq, i)) !== -1){
      count++;
      if(!first){
        try {
          var r = document.createRange();
          r.setStart(n, i); r.setEnd(n, Math.min(i + q.length, t.length));
          first = r;
        } catch(e) {}
      }
      i += Math.max(q.length, 1);
    }
  }
  if(first){
    var s = window.getSelection(); s.removeAllRanges(); s.addRange(first);
  }
  return JSON.stringify({count: count});
})()
""";

    // Lanjut/mundur dari sorotan aktif (dipakai find bar M3)
    private const string FindNextJs = "window.find(%Q%, false, %BACK%, true, false, false, false)";

    private async Task<string> McpFindAsync(JsonElement args)
    {
        var tab = ResolveTab(args);
        WakeTab(tab);
        if (tab.Web?.CoreWebView2 is null) throw new McpToolError("Engine tab belum siap");
        var query = ArgStr(args, "query") ?? "";
        var js = FindJs.Replace("%Q%", JsonSerializer.Serialize(query));
        var raw = await tab.Web.CoreWebView2.ExecuteScriptAsync(js);
        var json = JsonSerializer.Deserialize<string>(raw) ?? "{\"count\":0}";
        AgentLog.Write("agent.find", new { url = tab.Web.CoreWebView2.Source, query });
        return json;
    }

    private async Task<string> McpEvaluateAsync(JsonElement args)
    {
        if (!AgentCfg.AllowEvaluate)
            throw new McpToolError(
                "flit_evaluate_js nonaktif. Nyalakan via agent.allowEvaluate=true di config.json atau --allow-evaluate.");
        var tab = ResolveTab(args);
        WakeTab(tab);
        if (tab.Web?.CoreWebView2 is null) throw new McpToolError("Engine tab belum siap");
        var url = tab.Web.CoreWebView2.Source ?? "";
        if (url.Length > 0 && !AgentCfg.IsDomainAllowed(url))
        {
            AgentLog.Write("agent.denied", new { action = "evaluate", url });
            throw new McpToolError($"Domain tidak diizinkan: {new Uri(url).Host}");
        }
        var script = ArgStr(args, "script") ?? throw new McpToolError("script wajib diisi");
        var raw = await tab.Web.CoreWebView2.ExecuteScriptAsync(script);
        AgentLog.Write("agent.evaluate", new { url, script });
        return "{\"result\":" + raw + "}"; // raw sudah JSON-encoded oleh WebView2
    }

    private string McpStatus()
    {
        return JsonSerializer.Serialize(new
        {
            name = "Flit",
            version = "0.3.0",
            milestone = "M3",
            tabs = _tabs.Count,
            active = _active,
            headless = Headless,
            agent = new
            {
                domainMode = AgentCfg.AllowedDomains.Count == 0 ? "all" : "allowlist",
                allowedDomains = AgentCfg.AllowedDomains,
                allowEvaluate = AgentCfg.AllowEvaluate,
                logDir = AgentLog.LogDir,
            },
            ram = _ram.Text,
        });
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
