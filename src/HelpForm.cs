using System;
using System.Drawing;
using System.Windows.Forms;

namespace DropOrb
{
    internal sealed class HelpForm : Form
    {
        public HelpForm(Point anchor)
        {
            Text = "DropOrb 功能说明";
            ClientSize = new Size(410, 560);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Background;
            TopMost = true;
            ShowInTaskbar = Program.InspectMode;
            StartPosition = FormStartPosition.Manual;
            Location = KeepOnScreen(anchor);
            Region = new Region(Theme.Rounded(new Rectangle(0, 0, Width, Height), 22));

            var title = new Label
            {
                Text = "DropOrb 使用说明",
                ForeColor = Theme.Text,
                Font = new Font("Microsoft YaHei UI", 15, FontStyle.Bold),
                AutoSize = false
            };
            title.SetBounds(22, 17, 280, 34);
            Controls.Add(title);

            var subtitle = new Label
            {
                Text = "少找菜单，把东西交给投递球",
                ForeColor = Theme.Muted,
                Font = new Font("Microsoft YaHei UI", 8),
                AutoSize = false
            };
            subtitle.SetBounds(22, 50, 300, 22);
            Controls.Add(subtitle);

            var close = Theme.ActionButton("×", "");
            close.Text = "×";
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.Padding = Padding.Empty;
            close.SetBounds(360, 17, 30, 30);
            close.Click += delegate { Close(); };
            Controls.Add(close);

            var body = new FlowLayoutPanel
            {
                AutoScroll = true,
                BackColor = Theme.Background,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 0, 7, 10)
            };
            body.SetBounds(20, 82, 376, 458);
            Controls.Add(body);

            body.Controls.Add(Section("最懒的用法",
                "Ctrl + Alt + D    处理当前剪贴板\r\n" +
                "Ctrl + Alt + Space    打开快捷命令\r\n" +
                "鼠标中键点球    同样处理剪贴板\r\n" +
                "直接拖进小球    展开环形快速投递\r\n" +
                "单击小球        打开临时架"));

            body.Controls.Add(Section("拖放加速键",
                "Shift + 拖入    立即执行第一项推荐动作\r\n" +
                "Ctrl + 拖入     不弹菜单，直接加入临时架\r\n" +
                "环形上/左/右    推荐、暂存、复制到桌面\r\n" +
                "环形下方        查看全部动作"));

            body.Controls.Add(Section("越用越顺手",
                "动作会按不同内容类型记录你的使用次数\r\n" +
                "常用动作会自动排到前面，不会自动乱执行\r\n" +
                "右键菜单可随时重置动作偏好"));

            body.Controls.Add(Section("图片与截图",
                "置顶预览 · 缩小 50% · PNG/JPG 转换\r\n" +
                "压缩副本 · 复制图片 · 打开所在位置\r\n" +
                "复制截图后按快捷键，也会按图片处理"));

            body.Controls.Add(Section("文件、PDF 与 ZIP",
                "打开 · 复制到桌面 · 打包 ZIP\r\n" +
                "计算 SHA-256 · 安全解压 · 预览压缩包\r\n" +
                "多文件可统一压缩、复制名称或完整路径"));

            body.Controls.Add(Section("文件夹",
                "打包 ZIP · 生成 TXT 目录树\r\n" +
                "复制第一层名称 · 复制完整路径\r\n" +
                "复制到桌面 · 加入临时架"));

            body.Controls.Add(Section("文字与链接",
                "清理首尾空白 · 整理成一行 · 提取网址\r\n" +
                "保存 TXT · 统计文本 · 打开或保存链接\r\n" +
                "净化链接可去掉常见广告跟踪参数"));

            body.Controls.Add(Section("快捷工具",
                "快捷命令可搜索并回车执行常用操作\r\n" +
                "可按需开启资源管理器右键“发送到 DropOrb”\r\n" +
                "右键菜单可打开下载文件夹、新建桌面便签\r\n" +
                "任务与撤销中心可查看后台工作和生成内容\r\n" +
                "也可按需开启开机自动启动\r\n" +
                "隐藏小球后，双击托盘图标即可恢复"));

            body.Controls.Add(Section("本地与安全",
                "不上传文件或历史记录，不移动原文件\r\n" +
                "转换、压缩和复制都会使用新文件名\r\n" +
                "临时架只保存引用，移除记录不会删除原件"));

            KeyPreview = true;
            KeyDown += delegate(object sender, KeyEventArgs args) { if (args.KeyCode == Keys.Escape) Close(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var pen = new Pen(Theme.Border))
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width - 1, Height - 1), 22))
                e.Graphics.DrawPath(pen, path);
        }

        private static Panel Section(string heading, string content)
        {
            var lineCount = content.Split(new[] { "\r\n" }, StringSplitOptions.None).Length;
            var panel = new Panel
            {
                BackColor = Theme.Surface,
                Margin = new Padding(0, 0, 0, 10),
                Size = new Size(349, 49 + lineCount * 23)
            };
            var title = new Label
            {
                Text = heading,
                ForeColor = Theme.Cyan,
                Font = new Font("Microsoft YaHei UI", 9, FontStyle.Bold),
                AutoSize = false
            };
            title.SetBounds(14, 11, 315, 25);
            panel.Controls.Add(title);
            var description = new Label
            {
                Text = content,
                ForeColor = Theme.Secondary,
                Font = new Font("Microsoft YaHei UI", 8.3f),
                AutoSize = false,
                UseMnemonic = false
            };
            description.SetBounds(14, 38, 320, lineCount * 23 + 4);
            panel.Controls.Add(description);
            return panel;
        }

        private Point KeepOnScreen(Point desired)
        {
            var screen = Screen.FromPoint(desired).WorkingArea;
            return new Point(
                Math.Max(screen.Left + 8, Math.Min(desired.X, screen.Right - ClientSize.Width - 8)),
                Math.Max(screen.Top + 8, Math.Min(desired.Y, screen.Bottom - ClientSize.Height - 8)));
        }
    }
}
