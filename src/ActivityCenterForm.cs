using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DropOrb
{
    internal sealed class ActivityCenterForm : Form
    {
        private readonly JobManager jobs;
        private readonly UndoStore undo;
        private readonly ListBox jobList;
        private readonly ListBox undoList;

        public ActivityCenterForm(JobManager jobManager, UndoStore undoStore, Point anchor)
        {
            jobs = jobManager;
            undo = undoStore;
            Text = "DropOrb 任务与撤销";
            ClientSize = new Size(410, 500);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Background;
            TopMost = true;
            ShowInTaskbar = Program.InspectMode;
            StartPosition = FormStartPosition.Manual;
            Location = KeepOnScreen(anchor);
            Region = new Region(Theme.Rounded(new Rectangle(0, 0, Width, Height), 22));

            AddLabel("任务与撤销", 20, 16, 15, Theme.Text, 280, 34, FontStyle.Bold);
            AddLabel("后台工作不中断，生成内容可撤销", 20, 49, 7.5f, Theme.Muted, 300, 22, FontStyle.Regular);
            var close = SmallButton("×", 360, 17, 30);
            close.Click += delegate { Close(); };

            AddLabel("后台任务", 20, 82, 9, Theme.Cyan, 160, 24, FontStyle.Bold);
            jobList = CreateList(20, 109, 370, 135);
            var open = SmallButton("打开输出", 20, 254, 90);
            open.Click += delegate { OpenSelectedOutput(); };

            AddLabel("可撤销的生成内容", 20, 307, 9, Theme.Cyan, 220, 24, FontStyle.Bold);
            undoList = CreateList(20, 334, 370, 105);
            var undoButton = SmallButton("撤销所选", 20, 449, 90);
            undoButton.Click += delegate { UndoSelected(); };
            AddLabel("只会删除 DropOrb 新生成的内容", 122, 455, 7.5f, Theme.Muted, 250, 22, FontStyle.Regular);

            jobs.Changed += OnChanged;
            undo.Changed += OnChanged;
            FormClosed += delegate
            {
                jobs.Changed -= OnChanged;
                undo.Changed -= OnChanged;
            };
            ReloadLists();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Theme.Border))
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 22))
                e.Graphics.DrawPath(pen, path);
        }

        private void OnChanged(object sender, EventArgs args)
        {
            if (IsHandleCreated) BeginInvoke(new Action(ReloadLists));
        }

        private void ReloadLists()
        {
            jobList.Items.Clear();
            foreach (var job in jobs.Jobs) jobList.Items.Add(job);
            if (jobList.Items.Count == 0) jobList.Items.Add("还没有后台任务");

            undoList.Items.Clear();
            foreach (var entry in undo.Entries) undoList.Items.Add(entry);
            if (undoList.Items.Count == 0) undoList.Items.Add("还没有可撤销内容");
        }

        private void OpenSelectedOutput()
        {
            var job = jobList.SelectedItem as JobEntry;
            if (job == null || job.Outputs.Count == 0) return;
            var path = job.Outputs[0];
            if (Directory.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            else if (File.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }

        private void UndoSelected()
        {
            var entry = undoList.SelectedItem as UndoEntry;
            if (entry == null) return;
            var names = string.Join(Environment.NewLine, entry.Paths.ConvertAll(Path.GetFileName).ToArray());
            if (MessageBox.Show(this, "将删除这些由 DropOrb 生成的内容：\r\n\r\n" + names, "确认撤销", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;
            try { undo.Undo(entry); }
            catch (Exception error) { MessageBox.Show(this, error.Message, "无法撤销", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private ListBox CreateList(int x, int y, int width, int height)
        {
            var list = new ListBox
            {
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 8.3f),
                IntegralHeight = false,
                ItemHeight = 27
            };
            list.SetBounds(x, y, width, height);
            Controls.Add(list);
            return list;
        }

        private Button SmallButton(string text, int x, int y, int width)
        {
            var button = Theme.ActionButton(text, "");
            button.Text = text;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = Padding.Empty;
            button.SetBounds(x, y, width, 32);
            Controls.Add(button);
            return button;
        }

        private void AddLabel(string text, int x, int y, float size, Color color, int width, int height, FontStyle style)
        {
            var label = new Label { Text = text, ForeColor = color, Font = new Font("Microsoft YaHei UI", size, style), AutoSize = false };
            label.SetBounds(x, y, width, height);
            Controls.Add(label);
        }

        private Point KeepOnScreen(Point desired)
        {
            var screen = Screen.FromPoint(desired).WorkingArea;
            return new Point(Math.Max(screen.Left + 8, Math.Min(desired.X, screen.Right - ClientSize.Width - 8)), Math.Max(screen.Top + 8, Math.Min(desired.Y, screen.Bottom - ClientSize.Height - 8)));
        }
    }
}
