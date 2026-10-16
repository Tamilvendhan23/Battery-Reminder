using System.Drawing.Drawing2D;
using System.Drawing.Text;
using BatteryReminder.Services;

namespace BatteryReminder.UI
{
    /// <summary>
    /// Always-on-top banner that appears above every window (including full-screen remote/VDI sessions)
    /// without taking keyboard focus. Click it to dismiss.
    /// </summary>
    public sealed class AlertPopup : Form
    {
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private readonly System.Windows.Forms.Timer _keepOnTop = new() { Interval = 1500 };
        private readonly System.Windows.Forms.Timer _autoHide = new();

        private string _title = string.Empty;
        private string _message = string.Empty;
        private int _percent = -1;
        private bool _flash;
        private Rectangle _area;

        public AlertKind Kind { get; private set; } = AlertKind.Info;

        public AlertPopup()
        {
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            ClientSize = new Size(440, 112);
            BackColor = Color.FromArgb(31, 41, 55);
            Cursor = Cursors.Hand;

            _keepOnTop.Tick += (_, _) =>
            {
                if (Kind == AlertKind.Critical) { _flash = !_flash; Invalidate(); }
                if (IsHandleCreated) NativeMethods.KeepOnTop(Handle); // some full-screen apps grab topmost back
            };
            _autoHide.Tick += (_, _) => Close();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        public void SetContent(AlertKind kind, string title, string message, int percent, int autoHideSeconds)
        {
            Kind = kind;
            _title = title;
            _message = message;
            _percent = percent;
            _flash = false;

            _autoHide.Stop();
            if (autoHideSeconds > 0)
            {
                _autoHide.Interval = autoHideSeconds * 1000;
                _autoHide.Start();
            }
            Invalidate();
        }

        public void UpdatePercent(int percent)
        {
            if (_percent == percent) return;
            _percent = percent;
            Invalidate();
        }

        /// <summary>Shows the banner top-centre on the monitor where the mouse is (that's where you're working).</summary>
        public void Present()
        {
            _area = Screen.FromPoint(Cursor.Position).WorkingArea;
            _ = Handle; // create the window so DPI scaling is applied before positioning
            Reposition();
            if (!Visible) Show();
            NativeMethods.KeepOnTop(Handle);
            _keepOnTop.Start();
        }

        private void Reposition()
        {
            if (_area.IsEmpty) return;
            int margin = (int)(16 * DeviceDpi / 96f);
            Location = new Point(_area.Left + (_area.Width - Width) / 2, _area.Top + margin);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            Reposition();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            float s = DeviceDpi / 96f;
            using var path = new GraphicsPath();
            float d = 24 * s;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(Width - d, 0, d, d, 270, 90);
            path.AddArc(Width - d, Height - d, d, d, 0, 90);
            path.AddArc(0, Height - d, d, d, 90, 90);
            path.CloseFigure();
            Region?.Dispose();
            Region = new Region(path);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Close();
        }

        private Color Accent => Kind switch
        {
            AlertKind.Critical => _flash ? Color.FromArgb(255, 120, 120) : Palette.Bad,
            AlertKind.Low => Palette.Warn,
            AlertKind.High => Palette.Good,
            _ => Palette.Info
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);

            float s = DeviceDpi / 96f;
            Color accent = Accent;

            using (var bar = new SolidBrush(accent)) g.FillRectangle(bar, 0, 0, 9 * s, Height);
            using (var border = new Pen(accent, 2 * s)) g.DrawRectangle(border, s, s, Width - 2 * s, Height - 2 * s);

            float left = 28 * s, right = Width - 18 * s;
            string pct = _percent >= 0 ? $"{_percent}%" : string.Empty;

            using var pctFont = new Font("Segoe UI", 26f, FontStyle.Bold);
            using var titleFont = new Font("Segoe UI", 13f, FontStyle.Bold);
            using var msgFont = new Font("Segoe UI", 10f);
            using var hintFont = new Font("Segoe UI", 8f);
            using var white = new SolidBrush(Color.White);
            using var grey = new SolidBrush(Color.FromArgb(203, 213, 225));
            using var dim = new SolidBrush(Color.FromArgb(120, 135, 155));
            using var accentBrush = new SolidBrush(accent);
            using var fmt = new StringFormat { Trimming = StringTrimming.EllipsisWord };

            float textRight = right;
            if (pct.Length > 0)
            {
                SizeF ps = g.MeasureString(pct, pctFont);
                g.DrawString(pct, pctFont, accentBrush, right - ps.Width, (Height - ps.Height) / 2f);
                textRight = right - ps.Width - 12 * s;
            }

            float titleH = titleFont.GetHeight(g);
            g.DrawString(_title, titleFont, white, new RectangleF(left, 14 * s, textRight - left, titleH + 2), fmt);
            g.DrawString(_message, msgFont, grey,
                new RectangleF(left, 14 * s + titleH + 4 * s, textRight - left, 42 * s), fmt);

            if (Kind != AlertKind.Info)
                g.DrawString("Click to dismiss", hintFont, dim, left, Height - 22 * s);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _keepOnTop.Stop();
            _autoHide.Stop();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _keepOnTop.Dispose();
                _autoHide.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
