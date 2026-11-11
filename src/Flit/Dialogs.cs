namespace Flit;

/// <summary>Jendela riwayat (M3) — daftar terbaru dulu + filter teks + hapus semua.</summary>
public class HistoryForm : Form
{
    private readonly ListView _list;
    private readonly TextBox _filter;
    private List<HistoryStore.Entry> _all;

    public HistoryForm(Action<string> openUrl)
    {
        Text = "History — Flit";
        ClientSize = new Size(760, 520);
        BackColor = Color.FromArgb(32, 33, 36);
        ForeColor = Color.FromArgb(232, 234, 237);
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        _all = HistoryStore.ReadLatest(10_000);

        var top = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 8, 8, 0), BackColor = Color.FromArgb(32, 33, 36) };
        _filter = new TextBox
        {
            Width = 320,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(60, 60, 64),
            ForeColor = Color.FromArgb(232, 234, 237),
        };
        _filter.PlaceholderText = "search title/URL…";
        _filter.TextChanged += (s, e) => Reload();
        var clear = new Button
        {
            Text = "Clear all",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 64),
            ForeColor = Color.FromArgb(232, 234, 237),
            Width = 100,
            Left = 340,
            Height = 24,
            Top = 8,
        };
        clear.FlatAppearance.BorderSize = 0;
        clear.Click += (s, e) =>
        {
            if (MessageBox.Show(this, "Clear ALL history?", "Flit", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                HistoryStore.Clear();
                _all = new List<HistoryStore.Entry>();
                Reload();
            }
        };
        top.Controls.Add(_filter);
        top.Controls.Add(clear);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            BackColor = Color.FromArgb(43, 43, 43),
            ForeColor = Color.FromArgb(232, 234, 237),
            BorderStyle = BorderStyle.None,
        };
        _list.Columns.Add("Time", 130);
        _list.Columns.Add("Title", 240);
        _list.Columns.Add("URL", 360);
        _list.DoubleClick += (s, e) =>
        {
            if (_list.SelectedItems.Count > 0)
            {
                var url = _list.SelectedItems[0].SubItems[2].Text;
                openUrl(url);
                Close();
            }
        };

        Controls.Add(_list);
        Controls.Add(top);
        Reload();

        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
            if (e.Control && e.KeyCode == Keys.F) { _filter.Focus(); _filter.SelectAll(); }
        };
    }

    private void Reload()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        var q = _filter.Text.Trim();
        int shown = 0;
        foreach (var e in _all)
        {
            if (shown >= 2000) break; // jaga UI tetap responsif
            if (q.Length > 0 &&
                !e.Title.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !e.Url.Contains(q, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var it = new ListViewItem(e.Time);
            it.SubItems.Add(e.Title);
            it.SubItems.Add(e.Url);
            _list.Items.Add(it);
            shown++;
        }
        _list.EndUpdate();
    }
}
