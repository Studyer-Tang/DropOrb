using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DropOrb
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(9, 13, 24);
        public static readonly Color Surface = Color.FromArgb(18, 26, 43);
        public static readonly Color SurfaceHigh = Color.FromArgb(25, 36, 58);
        public static readonly Color Border = Color.FromArgb(49, 65, 91);
        public static readonly Color Text = Color.FromArgb(246, 248, 252);
        public static readonly Color Secondary = Color.FromArgb(160, 174, 196);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Blue = Color.FromArgb(96, 165, 250);
        public static readonly Color Violet = Color.FromArgb(167, 139, 250);
        public static readonly Color Cyan = Color.FromArgb(94, 234, 212);

        public static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Button ActionButton(string title, string hint)
        {
            var button = new Button
            {
                Text = title + "\r\n" + hint,
                FlatStyle = FlatStyle.Flat,
                BackColor = SurfaceHigh,
                ForeColor = Text,
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 8, 0),
                Cursor = Cursors.Hand,
                TabStop = true
            };
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(34, 49, 76);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 58, 88);
            return button;
        }
    }
}
