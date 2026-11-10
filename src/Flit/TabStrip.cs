namespace Flit;

// Tab bar digambar sendiri (owner-drawn) — tanpa kontrol standar, konsisten dark mode.
internal class TabStrip : Panel
{
    private readonly List<FlitTab> _tabs;

    public event Action<int>? TabSelected;
    public event Action<int>? TabClosed;
    public event Action? NewTabRequested;

    private int _active = -1;
    private int _hover = -1;
    private bool _hoverClose;
    private bool _hoverPlus;

    private const int PadL = 4, Gap = 2, CloseW = 22, PlusW = 30, MinW = 56, MaxW = 200;

    public TabStrip(List<FlitTab> tabs)
    {
        _tabs = tabs;
        Dock = DockStyle.Top;
        Height = 34;
        BackColor = Color.FromArgb(32, 33, 36);
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw, true);
    }

    public void SetActive(int index)
    {
        _active = index;
        Invalidate();
    }

    // Lebar tab menyusut saat tab bertambah (ala Firefox), dengan batas minimum
    private int TabW
    {
        get
        {
            if (_tabs.Count == 0) return MaxW;
            int avail = Width - PadL - PlusW - (_tabs.Count - 1) * Gap - 8;
            return Math.Max(MinW, Math.Min(MaxW, avail / _tabs.Count));
        }
    }

    private Rectangle RectOf(int i)
    {
        int w = TabW;
        return new Rectangle(PadL + i * (w + Gap), 3, w, Height - 6);
    }

    private Rectangle PlusRect => new(PadL + _tabs.Count * (TabW + Gap), 3, PlusW, Height - 6);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        Color activeBack = Color.FromArgb(60, 60, 64);
        Color hoverBack = Color.FromArgb(52, 52, 56);
        Color back = Color.FromArgb(43, 43, 43);
        Color fg = Color.FromArgb(232, 234, 237);
        Color dim = Color.FromArgb(154, 160, 166);
        Color accent = Color.FromArgb(138, 180, 248);

        for (int i = 0; i < _tabs.Count; i++)
        {
            var r = RectOf(i);
            using (var b = new SolidBrush(i == _active ? activeBack : (i == _hover ? hoverBack : back)))
                g.FillRectangle(b, r);
            if (i == _active)
                using (var ab = new SolidBrush(accent))
                    g.FillRectangle(ab, r.X, r.Y, r.Width, 2);

            var tab = _tabs[i];
            // Glyph ASCII aman untuk GDI: "zzz" = tertidur, "…" = sedang memuat
            string glyph = tab.IsSleeping ? "zzz " : (tab.IsLoading ? "… " : string.Empty);
            string label = glyph + tab.Title;

            var textRect = new Rectangle(r.X + 6, r.Y, r.Width - CloseW - 10, r.Height);
            TextRenderer.DrawText(g, label, Font, textRect, i == _active ? fg : dim,
                TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(g, "✕", Font,
                new Rectangle(r.Right - CloseW, r.Y, CloseW, r.Height),
                (i == _hover && _hoverClose) ? fg : dim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        TextRenderer.DrawText(g, "＋", Font, PlusRect, _hoverPlus ? fg : dim,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }

    private int HitTab(int x, out bool close)
    {
        close = false;
        for (int i = 0; i < _tabs.Count; i++)
        {
            var r = RectOf(i);
            if (x >= r.X && x < r.Right)
            {
                close = x >= r.Right - CloseW;
                return i;
            }
        }
        return -1;
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;
        int i = HitTab(e.X, out bool close);
        if (i >= 0)
        {
            if (close) TabClosed?.Invoke(i);
            else TabSelected?.Invoke(i);
        }
        else if (PlusRect.Contains(e.Location))
        {
            NewTabRequested?.Invoke();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = HitTab(e.X, out bool close);
        bool plus = PlusRect.Contains(e.Location);
        if (i != _hover || close != _hoverClose || plus != _hoverPlus)
        {
            _hover = i;
            _hoverClose = close;
            _hoverPlus = plus;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        _hoverClose = false;
        _hoverPlus = false;
        Invalidate();
    }
}
