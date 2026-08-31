using System;
using System.Drawing;
using System.Windows.Forms;

namespace DropOrb
{
    internal sealed class PinnedImageForm : Form
    {
        private readonly Image image;
        private Point dragStart;

        public PinnedImageForm(string path)
        {
            image = Image.FromFile(path);
            Text = "DropOrb Pinned Image";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Color.Black;
            TopMost = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            var work = Screen.PrimaryScreen.WorkingArea;
            var scale = Math.Min(1.0, Math.Min(620.0 / image.Width, 460.0 / image.Height));
            ClientSize = new Size(Math.Max(80, (int)(image.Width * scale)), Math.Max(60, (int)(image.Height * scale)));
            Opacity = 0.96;
            var menu = new ContextMenuStrip();
            var copy = menu.Items.Add("复制图片");
            var close = menu.Items.Add("关闭置顶图片");
            copy.Click += delegate { Clipboard.SetImage(new Bitmap(image)); };
            close.Click += delegate { Close(); };
            ContextMenuStrip = menu;
            MouseDown += delegate(object sender, MouseEventArgs args) { if (args.Button == MouseButtons.Left) dragStart = args.Location; };
            MouseMove += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button == MouseButtons.Left) Location = new Point(Left + args.X - dragStart.X, Top + args.Y - dragStart.Y);
            };
            DoubleClick += delegate { Close(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(image, ClientRectangle);
            using (var border = new Pen(Color.FromArgb(120, Theme.Blue), 2)) e.Graphics.DrawRectangle(border, 1, 1, Width - 3, Height - 3);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) image.Dispose();
            base.Dispose(disposing);
        }
    }
}
