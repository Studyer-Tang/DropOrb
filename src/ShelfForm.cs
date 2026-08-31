using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DropOrb
{
    internal sealed class ShelfForm : Form
    {
        private readonly ShelfStore store;
        private readonly ListBox list;

        public ShelfForm(ShelfStore shelfStore, Point anchor)
        {
            store = shelfStore;
            Text = "DropOrb 临时架";
            ClientSize = new Size(360, 420);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Background;
            TopMost = true;
            ShowInTaskbar = Program.InspectMode;
            StartPosition = FormStartPosition.Manual;
            Location = KeepOnScreen(anchor);
            Region = new Region(Theme.Rounded(new Rectangle(0, 0, Width, Height), 22));

            var title = new Label { Text = "临时架", ForeColor = Theme.Text, Font = new Font("Microsoft YaHei UI", 15, FontStyle.Bold), AutoSize = false };
            title.SetBounds(20, 18, 160, 32);
            Controls.Add(title);
            var subtitle = new Label { Text = "只保存引用，不移动原文件", ForeColor = Theme.Muted, Font = new Font("Microsoft YaHei UI", 7.5f), AutoSize = false };
            subtitle.SetBounds(20, 50, 230, 20);
            Controls.Add(subtitle);
            var close = Theme.ActionButton("×", "");
            close.Text = "×";
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.Padding = Padding.Empty;
            close.SetBounds(315, 16, 28, 28);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            list = new ListBox
            {
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 9),
                IntegralHeight = false,
                ItemHeight = 30
            };
            list.SetBounds(20, 80, 320, 270);
            list.DoubleClick += delegate { OpenSelected(); };
            Controls.Add(list);

            var open = SmallButton("打开", 20);
            var copy = SmallButton("复制", 105);
            var remove = SmallButton("移除记录", 190);
            open.Click += delegate { OpenSelected(); };
            copy.Click += delegate { CopySelected(); };
            remove.Click += delegate { RemoveSelected(); };
            Controls.Add(open);
            Controls.Add(copy);
            Controls.Add(remove);
            Reload();
            Deactivate += delegate { if (!ContainsFocus) Close(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Theme.Border)) using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 22)) e.Graphics.DrawPath(pen, path);
        }

        private Button SmallButton(string text, int x)
        {
            var button = Theme.ActionButton(text, "");
            button.Text = text;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = Padding.Empty;
            button.SetBounds(x, 366, text.Length > 4 ? 96 : 72, 34);
            return button;
        }

        private void Reload()
        {
            list.Items.Clear();
            foreach (var entry in store.Entries) list.Items.Add(entry);
            if (list.Items.Count == 0) list.Items.Add("还没有东西，拖到投递球就能暂存。 ");
        }

        private ShelfEntry SelectedEntry { get { return list.SelectedItem as ShelfEntry; } }

        private void OpenSelected()
        {
            var entry = SelectedEntry;
            if (entry == null) return;
            if (entry.Kind == "text") Clipboard.SetText(entry.Value);
            else Process.Start(new ProcessStartInfo(entry.Value) { UseShellExecute = true });
        }

        private void CopySelected()
        {
            var entry = SelectedEntry;
            if (entry != null) Clipboard.SetText(entry.Value);
        }

        private void RemoveSelected()
        {
            var entry = SelectedEntry;
            if (entry == null) return;
            store.Remove(entry);
            Reload();
        }

        private Point KeepOnScreen(Point desired)
        {
            var screen = Screen.FromPoint(desired).WorkingArea;
            return new Point(Math.Max(screen.Left + 8, Math.Min(desired.X, screen.Right - ClientSize.Width - 8)), Math.Max(screen.Top + 8, Math.Min(desired.Y, screen.Bottom - ClientSize.Height - 8)));
        }
    }
}
