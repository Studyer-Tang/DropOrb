using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DropOrb
{
    internal sealed class ActionPanelForm : Form
    {
        private readonly DropItem item;
        private readonly IList<ActionSpec> actions;
        private readonly JobManager jobs;
        private readonly Action<ActionSpec> onExecuted;

        public ActionPanelForm(DropItem dropItem, IList<ActionSpec> actionList, Point anchor, JobManager jobManager, Action<ActionSpec> executed)
        {
            item = dropItem;
            actions = actionList;
            jobs = jobManager;
            onExecuted = executed;
            var visibleActionCount = Math.Min(6, actions.Count);
            Text = "DropOrb Actions";
            ClientSize = new Size(338, 116 + visibleActionCount * 59);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Background;
            TopMost = true;
            ShowInTaskbar = Program.InspectMode;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            Padding = new Padding(18);
            Region = new Region(Theme.Rounded(new Rectangle(0, 0, Width, Height), 22));
            Location = KeepOnScreen(anchor);

            var kind = new Label
            {
                Text = item.KindLabel.ToUpperInvariant(),
                ForeColor = Theme.Cyan,
                Font = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Bold),
                AutoSize = false
            };
            kind.SetBounds(20, 18, 230, 20);
            Controls.Add(kind);

            var title = new Label
            {
                Text = item.DisplayName,
                ForeColor = Theme.Text,
                Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
                AutoEllipsis = true,
                AutoSize = false
            };
            title.SetBounds(20, 39, 276, 31);
            Controls.Add(title);

            var close = new Button
            {
                Text = "×",
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Surface,
                ForeColor = Theme.Secondary,
                Font = new Font("Segoe UI", 12),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            close.FlatAppearance.BorderSize = 0;
            close.SetBounds(298, 15, 28, 28);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            var prompt = new Label
            {
                Text = "想怎么处理？",
                ForeColor = Theme.Muted,
                Font = new Font("Microsoft YaHei UI", 8),
                AutoSize = false
            };
            prompt.SetBounds(20, 72, 180, 21);
            Controls.Add(prompt);

            for (var index = 0; index < visibleActionCount; index++)
            {
                var action = actions[index];
                var button = Theme.ActionButton(action.Title, action.Hint);
                button.SetBounds(20, 98 + index * 59, 298, 50);
                button.Click += delegate(object sender, EventArgs args) { RunAction(action, (Button)sender); };
                Controls.Add(button);
            }

            Deactivate += delegate { if (!ContainsFocus) Close(); };
            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs args) { if (args.KeyCode == Keys.Escape) Close(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var border = new Pen(Theme.Border))
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 22)) e.Graphics.DrawPath(border, path);
        }

        private void RunAction(ActionSpec action, Button button)
        {
            var oldText = button.Text;
            try
            {
                if (action.IsBackground)
                {
                    jobs.Enqueue(action.Title, () => action.BackgroundExecute(item));
                    if (onExecuted != null) onExecuted(action);
                    Close();
                    return;
                }
                button.Enabled = false;
                button.Text = "正在处理…";
                Cursor = Cursors.WaitCursor;
                action.Execute(item, this);
                if (onExecuted != null) onExecuted(action);
                Close();
            }
            catch (Exception error)
            {
                button.Enabled = true;
                button.Text = oldText;
                Cursor = Cursors.Default;
                MessageBox.Show(this, error.Message, "DropOrb 处理失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private Size WidthHeight { get { return new Size(Width, Height); } }

        private Point KeepOnScreen(Point desired)
        {
            var screen = Screen.FromPoint(desired).WorkingArea;
            var x = Math.Max(screen.Left + 8, Math.Min(desired.X, screen.Right - ClientSize.Width - 8));
            var y = Math.Max(screen.Top + 8, Math.Min(desired.Y, screen.Bottom - ClientSize.Height - 8));
            return new Point(x, y);
        }
    }
}
