using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DropOrb
{
    internal sealed class QuickCommand
    {
        public string Title { get; private set; }
        public string Hint { get; private set; }
        public Action Execute { get; private set; }

        public QuickCommand(string title, string hint, Action execute)
        {
            Title = title;
            Hint = hint;
            Execute = execute;
        }
    }

    internal sealed class QuickCommandForm : Form
    {
        private readonly IList<QuickCommand> commands;
        private readonly TextBox search;
        private readonly Label searchHint;
        private readonly ListBox results;

        public QuickCommandForm(IList<QuickCommand> commands)
        {
            this.commands = commands;
            Text = "DropOrb 快捷命令";
            ClientSize = new Size(430, 390);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            TopMost = true;
            ShowInTaskbar = Program.InspectMode;
            StartPosition = FormStartPosition.Manual;
            KeyPreview = true;
            Region = new Region(Theme.Rounded(new Rectangle(0, 0, Width, Height), 22));

            var title = new Label { Text = "想做什么？", ForeColor = Theme.Text, Font = new Font("Microsoft YaHei UI", 15, FontStyle.Bold) };
            title.SetBounds(22, 17, 250, 32);
            Controls.Add(title);

            var shortcut = new Label { Text = "Ctrl + Alt + Space", ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleRight };
            shortcut.SetBounds(260, 21, 146, 24);
            Controls.Add(shortcut);

            search = new TextBox
            {
                BackColor = Theme.SurfaceHigh,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 11)
            };
            search.SetBounds(22, 60, 384, 34);
            Controls.Add(search);

            searchHint = new Label
            {
                Text = "输入：剪贴板、下载、便签……",
                ForeColor = Theme.Muted,
                BackColor = Theme.SurfaceHigh,
                Font = new Font("Microsoft YaHei UI", 7.5f),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Cursor = Cursors.IBeam
            };
            searchHint.SetBounds(190, 65, 208, 24);
            searchHint.Click += delegate { search.Focus(); };
            Controls.Add(searchHint);
            searchHint.BringToFront();

            results = new ListBox
            {
                BackColor = Theme.Background,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.None,
                Font = new Font("Microsoft YaHei UI", 10),
                ItemHeight = 34,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = false
            };
            results.SetBounds(22, 108, 384, 254);
            Controls.Add(results);

            search.TextChanged += delegate { searchHint.Visible = search.TextLength == 0; RefreshResults(); };
            search.KeyDown += OnInputKeyDown;
            results.KeyDown += OnInputKeyDown;
            results.DoubleClick += delegate { RunSelected(); };
            results.DrawItem += DrawCommand;
            Deactivate += delegate { if (!Program.InspectMode) Close(); };
            Shown += delegate { RefreshResults(); search.Focus(); };
            KeyDown += delegate(object sender, KeyEventArgs args) { if (args.KeyCode == Keys.Escape) Close(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Theme.Border))
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 22)) e.Graphics.DrawPath(pen, path);
        }

        private void RefreshResults()
        {
            var query = search.Text.Trim();
            var filtered = commands.Where(command => query.Length == 0 ||
                command.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                command.Hint.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            results.Items.Clear();
            results.Items.AddRange(filtered);
            if (results.Items.Count > 0)
            {
                results.SelectedIndex = 0;
                results.TopIndex = 0;
            }
        }

        private void OnInputKeyDown(object sender, KeyEventArgs args)
        {
            if (args.KeyCode == Keys.Escape) { Close(); args.Handled = true; return; }
            if (args.KeyCode == Keys.Enter) { RunSelected(); args.Handled = true; return; }
            if (sender == search && (args.KeyCode == Keys.Down || args.KeyCode == Keys.Up) && results.Items.Count > 0)
            {
                var delta = args.KeyCode == Keys.Down ? 1 : -1;
                results.SelectedIndex = Math.Max(0, Math.Min(results.Items.Count - 1, results.SelectedIndex + delta));
                args.Handled = true;
            }
        }

        private void RunSelected()
        {
            var command = results.SelectedItem as QuickCommand;
            if (command == null) return;
            Close();
            command.Execute();
        }

        private static void DrawCommand(object sender, DrawItemEventArgs args)
        {
            if (args.Index < 0) return;
            var list = (ListBox)sender;
            var command = (QuickCommand)list.Items[args.Index];
            var selected = (args.State & DrawItemState.Selected) != 0;
            using (var fill = new SolidBrush(selected ? Theme.SurfaceHigh : Theme.Background)) args.Graphics.FillRectangle(fill, args.Bounds);
            using (var title = new Font("Microsoft YaHei UI", 9.5f, FontStyle.Bold))
            using (var hint = new Font("Microsoft YaHei UI", 7.5f))
            using (var titleBrush = new SolidBrush(selected ? Theme.Cyan : Theme.Text))
            using (var hintBrush = new SolidBrush(Theme.Secondary))
            {
                args.Graphics.DrawString(command.Title, title, titleBrush, args.Bounds.X + 10, args.Bounds.Y + 1);
                args.Graphics.DrawString(command.Hint, hint, hintBrush, args.Bounds.X + 10, args.Bounds.Y + 19);
            }
        }
    }
}
