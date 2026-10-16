using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using BatteryReminder.Models;
using BatteryReminder.Services;

namespace BatteryReminder.UI
{
    /// <summary>Renders the tray / window icon in memory. Every native handle is released (no GDI leaks).</summary>
    public static class IconHelper
    {
        private const int IconSize = 32;

        public static Icon Create(BatteryInfo info, AppSettings s)
        {
            using var bmp = new Bitmap(IconSize, IconSize, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);

                int pct = info.Percentage;
                Color fill = info.HasReading
                    ? Palette.Level(pct, info.IsCharging, s.LowBatteryThreshold, s.CriticalBatteryThreshold)
                    : Color.FromArgb(148, 163, 184);

                if (s.TrayStyle == TrayIconStyle.Number) DrawNumber(g, pct, fill);
                else DrawBattery(g, pct, info.IsCharging, fill);
            }

            IntPtr h = bmp.GetHicon();
            try
            {
                using var tmp = Icon.FromHandle(h);
                return (Icon)tmp.Clone(); // clone owns a private copy of the handle
            }
            finally
            {
                NativeMethods.DestroyIcon(h);
            }
        }

        private static void DrawBattery(Graphics g, int pct, bool charging, Color fill)
        {
            Color frame = Palette.TaskbarIsLight() ? Color.FromArgb(55, 65, 81) : Color.FromArgb(229, 231, 235);

            var body = new Rectangle(3, 8, 23, 16);
            using (var pen = new Pen(frame, 2f)) g.DrawRectangle(pen, body);
            using (var tip = new SolidBrush(frame)) g.FillRectangle(tip, 27, 12, 3, 8);

            float ratio = Math.Clamp(pct >= 0 ? pct : 50, 0, 100) / 100f;
            int w = Math.Max(1, (int)((body.Width - 3) * ratio));
            using (var b = new SolidBrush(fill)) g.FillRectangle(b, body.X + 2, body.Y + 2, w, body.Height - 3);

            if (charging)
            {
                PointF[] bolt = { new(16, 9), new(11, 17), new(15, 17), new(13, 23), new(20, 14), new(16, 14) };
                using var white = new SolidBrush(Color.White);
                using var edge = new Pen(Color.FromArgb(120, 0, 0, 0), 1f);
                g.FillPolygon(white, bolt);
                g.DrawPolygon(edge, bolt);
            }
        }

        private static void DrawNumber(Graphics g, int pct, Color fill)
        {
            using (var path = new GraphicsPath())
            {
                const int r = 8;
                path.AddArc(0, 0, r, r, 180, 90);
                path.AddArc(IconSize - r - 1, 0, r, r, 270, 90);
                path.AddArc(IconSize - r - 1, IconSize - r - 1, r, r, 0, 90);
                path.AddArc(0, IconSize - r - 1, r, r, 90, 90);
                path.CloseFigure();
                using var b = new SolidBrush(fill);
                g.FillPath(b, path);
            }

            string text = pct >= 0 ? pct.ToString() : "?";
            float em = text.Length >= 3 ? 15f : 20f;
            using var font = new Font("Segoe UI", em, FontStyle.Bold, GraphicsUnit.Pixel);
            using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using var white = new SolidBrush(Color.White);
            g.DrawString(text, font, white, new RectangleF(0, 1, IconSize, IconSize), fmt);
        }
    }
}
