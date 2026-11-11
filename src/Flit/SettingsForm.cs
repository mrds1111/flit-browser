namespace Flit;

/// <summary>
/// Jendela pengaturan (M6) — tulis config.json + terapkan live lewat callback OnApplied.
/// Sengaja ringkas: hanya hal yang aman diubah tanpa restart.
/// </summary>
public class SettingsForm : Form
{
    private readonly AgentConfig _cfg;
    private readonly CheckBox _adblock;
    private readonly CheckBox _restore;
    private readonly NumericUpDown _sleep;
    private readonly NumericUpDown _deep;

    /// <summary>Dipanggil setelah Save (di UI thread) — MainForm menerapkan nilai live.</summary>
    public Action<AgentConfig>? OnApplied;

    public SettingsForm(AgentConfig cfg)
    {
        _cfg = cfg;
        Text = "Settings — Flit";
        ClientSize = new Size(430, 260);
        BackColor = Color.FromArgb(32, 33, 36);
        ForeColor = Color.FromArgb(232, 234, 237);
        Font = new Font("Segoe UI", 9.5f);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.FixedDialog;

        var y = 16;
        _adblock = new CheckBox
        {
            Text = "Adblock (EasyList / EasyPrivacy / ABPindo)",
            Left = 16, Top = y, Width = 390,
            ForeColor = ForeColor,
            Checked = cfg.AdblockEnabled,
        };
        y += 30;
        _restore = new CheckBox
        {
            Text = "Reopen last session's tabs on startup",
            Left = 16, Top = y, Width = 390,
            ForeColor = ForeColor,
            Checked = cfg.RestoreSession,
        };
        y += 36;
        _sleep = NumField("Background tabs sleep after (seconds):", cfg.SleepAfterSec, 3, 86400, ref y);
        _deep = NumField("Sleeping tabs discarded after (seconds, 0 = never):", cfg.DeepAfterSec, 0, 86400, ref y);

        var save = new Button
        {
            Text = "Save",
            Left = 16, Top = y + 8, Width = 110, Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(60, 60, 64),
            ForeColor = ForeColor,
        };
        save.FlatAppearance.BorderSize = 0;
        save.Click += (s, e) =>
        {
            cfg.AdblockEnabled = _adblock.Checked;
            cfg.RestoreSession = _restore.Checked;
            cfg.SleepAfterSec = (int)_sleep.Value;
            cfg.DeepAfterSec = (int)_deep.Value;
            cfg.Save();
            OnApplied?.Invoke(cfg);
            Close();
        };
        var note = new Label
        {
            Text = "UA / timezone / proxy / profiles: use CLI flags — see MCP.md.",
            Left = 140, Top = y + 14, Width = 280, Height = 20,
            ForeColor = Color.FromArgb(154, 160, 166),
        };

        Controls.Add(_adblock);
        Controls.Add(_restore);
        Controls.Add(save);
        Controls.Add(note);

        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    private NumericUpDown NumField(string label, int value, int min, int max, ref int y)
    {
        var lb = new Label { Text = label, Left = 16, Top = y, Width = 390, ForeColor = ForeColor, Height = 20 };
        y += 22;
        var num = new NumericUpDown
        {
            Left = 16, Top = y, Width = 110,
            Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max),
            BackColor = Color.FromArgb(60, 60, 64),
            ForeColor = ForeColor,
            BorderStyle = BorderStyle.FixedSingle,
        };
        y += 34;
        Controls.Add(lb);
        Controls.Add(num);
        num.Tag = lb;
        return num;
    }
}
