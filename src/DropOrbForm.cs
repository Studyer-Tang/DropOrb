using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Collections.Generic;

namespace DropOrb
{
    internal sealed class DropOrbForm : Form
    {
        private readonly ShelfStore shelf;
        private readonly ActionEngine engine;
        private readonly PreferenceStore preferences;
        private readonly UndoStore undoStore;
        private readonly JobManager jobs;
        private readonly NotifyIcon trayIcon;
        private readonly string settingsPath;
        private ShelfForm shelfForm;
        private HelpForm helpForm;
        private ActivityCenterForm activityForm;
        private QuickCommandForm quickCommandForm;
        private readonly Timer animationTimer;
        private int animationAngle;
        private readonly Timer radialLeaveTimer;
        private bool radialMode;
        private Rectangle compactBounds;
        private Point radialHover;
        private DropItem radialItem;
        private System.Collections.Generic.IList<ActionSpec> radialActions;
        private bool dragHover;
        private bool allowExit;
        private Point mouseDown;
        private bool moved;
        private bool hotkeyRegistered;
        private bool commandHotkeyRegistered;

        private const int ClipboardHotkeyId = 0xD09;
        private const int CommandHotkeyId = 0xD0A;
        private const int WmHotkey = 0x0312;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, Keys key);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public DropOrbForm()
        {
            var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DropOrb");
            shelf = new ShelfStore();
            preferences = new PreferenceStore(dataDirectory);
            undoStore = new UndoStore(dataDirectory);
            jobs = new JobManager(undoStore);
            engine = new ActionEngine(shelf, preferences);
            settingsPath = Path.Combine(dataDirectory, "settings.json");
            Text = "DropOrb";
            ClientSize = new Size(78, 78);
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Background;
            TopMost = true;
            ShowInTaskbar = Program.InspectMode;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            AllowDrop = true;
            Opacity = 0.97;
            ApplyCircleRegion();
            Location = LoadLocation();

            var context = new ContextMenuStrip();
            var commandItem = context.Items.Add("快捷命令  Ctrl+Alt+Space");
            var helpItem = context.Items.Add("功能说明");
            var clipboardItem = context.Items.Add("处理剪贴板  Ctrl+Alt+D");
            var shelfItem = context.Items.Add("打开临时架");
            var activityItem = context.Items.Add("任务与撤销");
            var downloadsItem = context.Items.Add("打开下载文件夹");
            var noteItem = context.Items.Add("新建桌面便签");
            var startupItem = new ToolStripMenuItem("开机自动启动") { CheckOnClick = true };
            context.Items.Add(startupItem);
            var sendToItem = new ToolStripMenuItem("文件管理器发送到") { CheckOnClick = true };
            context.Items.Add(sendToItem);
            var resetPreferenceItem = context.Items.Add("重置动作偏好");
            var hideItem = context.Items.Add("隐藏投递球");
            context.Items.Add(new ToolStripSeparator());
            var exitItem = context.Items.Add("退出 DropOrb");
            commandItem.Click += delegate { ShowQuickCommands(); };
            helpItem.Click += delegate { ShowHelp(); };
            clipboardItem.Click += delegate { ProcessClipboard(); };
            shelfItem.Click += delegate { ShowShelf(); };
            activityItem.Click += delegate { ShowActivityCenter(); };
            downloadsItem.Click += delegate { OpenDownloads(); };
            noteItem.Click += delegate { CreateQuickNote(); };
            startupItem.Click += delegate { SetStartup(startupItem.Checked); };
            sendToItem.Click += delegate { SetSendTo(sendToItem.Checked); };
            resetPreferenceItem.Click += delegate { ResetPreferences(); };
            hideItem.Click += delegate { Hide(); };
            exitItem.Click += delegate { allowExit = true; Close(); };
            context.Opening += delegate
            {
                startupItem.Checked = IsStartupEnabled();
                sendToItem.Checked = SendToIntegration.IsInstalled;
                activityItem.Text = ActivityTitle();
            };
            ContextMenuStrip = context;

            var trayMenu = new ContextMenuStrip();
            var showTray = trayMenu.Items.Add("显示投递球");
            var commandTray = trayMenu.Items.Add("快捷命令  Ctrl+Alt+Space");
            var helpTray = trayMenu.Items.Add("功能说明");
            var clipboardTray = trayMenu.Items.Add("处理剪贴板  Ctrl+Alt+D");
            var shelfTray = trayMenu.Items.Add("临时架");
            var activityTray = trayMenu.Items.Add("任务与撤销");
            var downloadsTray = trayMenu.Items.Add("打开下载文件夹");
            var noteTray = trayMenu.Items.Add("新建桌面便签");
            var startupTray = new ToolStripMenuItem("开机自动启动") { CheckOnClick = true };
            trayMenu.Items.Add(startupTray);
            var sendToTray = new ToolStripMenuItem("文件管理器发送到") { CheckOnClick = true };
            trayMenu.Items.Add(sendToTray);
            trayMenu.Items.Add(new ToolStripSeparator());
            var exitTray = trayMenu.Items.Add("退出");
            showTray.Click += delegate { ShowOrb(); };
            commandTray.Click += delegate { ShowQuickCommands(); };
            helpTray.Click += delegate { ShowHelp(); };
            clipboardTray.Click += delegate { ProcessClipboard(); };
            shelfTray.Click += delegate { ShowShelf(); };
            activityTray.Click += delegate { ShowActivityCenter(); };
            downloadsTray.Click += delegate { OpenDownloads(); };
            noteTray.Click += delegate { CreateQuickNote(); };
            startupTray.Click += delegate { SetStartup(startupTray.Checked); };
            sendToTray.Click += delegate { SetSendTo(sendToTray.Checked); };
            exitTray.Click += delegate { allowExit = true; Close(); };
            trayMenu.Opening += delegate
            {
                startupTray.Checked = IsStartupEnabled();
                sendToTray.Checked = SendToIntegration.IsInstalled;
                activityTray.Text = ActivityTitle();
            };
            trayIcon = new NotifyIcon { Icon = SystemIcons.Application, Text = "DropOrb · 拖进来，马上处理", ContextMenuStrip = trayMenu, Visible = true };
            trayIcon.DoubleClick += delegate { ShowOrb(); };
            jobs.Completed += OnJobCompleted;
            animationTimer = new Timer { Interval = 90 };
            animationTimer.Tick += delegate
            {
                if (jobs.RunningCount == 0) return;
                animationAngle = (animationAngle + 12) % 360;
                Invalidate();
            };
            animationTimer.Start();
            radialLeaveTimer = new Timer { Interval = 140 };
            radialLeaveTimer.Tick += delegate
            {
                radialLeaveTimer.Stop();
                if (radialMode && !Bounds.Contains(Cursor.Position)) ExitRadialMode();
            };

            DragEnter += OnDragEnter;
            DragOver += OnDragOver;
            DragLeave += delegate { radialLeaveTimer.Stop(); radialLeaveTimer.Start(); };
            DragDrop += OnDragDrop;
            MouseDown += OnOrbMouseDown;
            MouseMove += OnOrbMouseMove;
            MouseUp += OnOrbMouseUp;
            FormClosing += OnFormClosing;
            FormClosed += OnFormClosed;
            Shown += delegate
            {
                trayIcon.BalloonTipTitle = "DropOrb 已启动";
                trayIcon.BalloonTipText = "拖入文件，或按 Ctrl+Alt+D 直接处理剪贴板。";
                trayIcon.ShowBalloonTip(2600);
                if (Program.InitialPaths != null && Program.InitialPaths.Length > 0) ProcessExternalArguments(Program.InitialPaths);
                if (Program.InspectHelp) ShowHelp();
                if (Program.InspectActivity) ShowActivityCenter();
                if (Program.InspectCommand) ShowQuickCommands();
                if (!string.IsNullOrWhiteSpace(Program.InspectRadialPath))
                {
                    var radialData = new DataObject();
                    radialData.SetData(DataFormats.FileDrop, new[] { Program.InspectRadialPath });
                    radialItem = DropItem.FromData(radialData);
                    radialActions = engine.GetActions(radialItem);
                    EnterRadialMode();
                }
                if (!string.IsNullOrWhiteSpace(Program.InspectDropPath))
                {
                    try
                    {
                        var data = new DataObject();
                        data.SetData(DataFormats.FileDrop, new[] { Program.InspectDropPath });
                        ShowActionPanel(DropItem.FromData(data));
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(this, error.Message, "DropOrb 检查失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (radialMode)
            {
                PaintRadial(e.Graphics);
                return;
            }
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var outer = new Rectangle(4, 4, 70, 70);
            using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(shadow, 5, 7, 68, 68);
            using (var gradient = new LinearGradientBrush(outer, dragHover ? Theme.Cyan : Theme.Blue, Theme.Violet, 45f)) g.FillEllipse(gradient, outer);
            using (var glow = new Pen(Color.FromArgb(dragHover ? 230 : 95, Color.White), dragHover ? 3f : 1.5f)) g.DrawEllipse(glow, outer);
            if (jobs.RunningCount > 0)
            {
                using (var progress = new Pen(Theme.Cyan, 3.5f))
                {
                    progress.StartCap = LineCap.Round;
                    progress.EndCap = LineCap.Round;
                    g.DrawArc(progress, 2, 2, 74, 74, animationAngle, 105);
                }
            }
            if (dragHover)
            {
                using (var font = new Font("Segoe UI", 20, FontStyle.Bold)) using (var brush = new SolidBrush(Color.White)) using (var format = Centered()) g.DrawString("+", font, brush, ClientRectangle, format);
            }
            else
            {
                using (var pen = new Pen(Color.White, 3))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 39, 22, 39, 50);
                    g.DrawLine(pen, 28, 39, 39, 50);
                    g.DrawLine(pen, 50, 39, 39, 50);
                }
                using (var font = new Font("Segoe UI", 6.5f, FontStyle.Bold)) using (var brush = new SolidBrush(Color.FromArgb(225, Color.White))) using (var format = Centered()) g.DrawString("DROP", font, brush, new Rectangle(10, 55, 58, 12), format);
            }
        }

        private void OnDragEnter(object sender, DragEventArgs args)
        {
            try
            {
                radialItem = DropItem.FromData(args.Data);
                radialActions = engine.GetActions(radialItem);
                args.Effect = DragDropEffects.Copy;
                dragHover = true;
                EnterRadialMode();
            }
            catch
            {
                args.Effect = DragDropEffects.None;
            }
        }

        private void OnDragOver(object sender, DragEventArgs args)
        {
            args.Effect = DragDropEffects.Copy;
            radialLeaveTimer.Stop();
            radialHover = PointToClient(new Point(args.X, args.Y));
            Invalidate();
        }

        private void OnDragDrop(object sender, DragEventArgs args)
        {
            var dropPoint = PointToClient(new Point(args.X, args.Y));
            dragHover = false;
            radialLeaveTimer.Stop();
            try
            {
                var item = radialItem ?? DropItem.FromData(args.Data);
                var actions = radialActions ?? engine.GetActions(item);
                var zone = radialMode ? RadialZone(dropPoint) : "more";
                ExitRadialMode();
                var modifiers = Control.ModifierKeys;
                if ((modifiers & Keys.Control) == Keys.Control)
                {
                    shelf.Add(item);
                    Toast("已加入临时架", "原文件没有移动。 ");
                    return;
                }
                if ((modifiers & Keys.Shift) == Keys.Shift && actions.Count > 0)
                {
                    ExecuteQuickAction(item, actions[0]);
                    return;
                }
                if (zone == "shelf")
                {
                    shelf.Add(item);
                    Toast("已加入临时架", "原文件没有移动。 ");
                }
                else if (zone == "recommend" && actions.Count > 0) ExecuteQuickAction(item, actions[0]);
                else if (zone == "desktop")
                {
                    var desktopAction = System.Linq.Enumerable.FirstOrDefault(actions, action => action.Title.IndexOf("桌面", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (desktopAction == null) ShowActionPanel(item, actions);
                    else ExecuteQuickAction(item, desktopAction);
                }
                else ShowActionPanel(item, actions);
            }
            catch (Exception error)
            {
                ExitRadialMode();
                MessageBox.Show(this, error.Message, "DropOrb 无法处理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                radialItem = null;
                radialActions = null;
            }
        }

        private void ShowActionPanel(DropItem item)
        {
            ShowActionPanel(item, engine.GetActions(item));
        }

        private void ShowActionPanel(DropItem item, System.Collections.Generic.IList<ActionSpec> actions)
        {
            var panel = new ActionPanelForm(item, actions, PanelAnchor(), jobs, action => engine.Remember(item.Kind, action));
            panel.Show();
            panel.Activate();
        }

        private void ExecuteQuickAction(DropItem item, ActionSpec action)
        {
            if (action.IsBackground) jobs.Enqueue(action.Title, () => action.BackgroundExecute(item));
            else action.Execute(item, this);
            engine.Remember(item.Kind, action);
        }

        private void EnterRadialMode()
        {
            if (radialMode) return;
            compactBounds = Bounds;
            var work = Screen.FromRectangle(compactBounds).WorkingArea;
            var center = new Point(compactBounds.Left + compactBounds.Width / 2, compactBounds.Top + compactBounds.Height / 2);
            var x = Math.Max(work.Left + 6, Math.Min(center.X - 125, work.Right - 256));
            var y = Math.Max(work.Top + 6, Math.Min(center.Y - 125, work.Bottom - 256));
            radialMode = true;
            Bounds = new Rectangle(x, y, 250, 250);
            using (var path = Theme.Rounded(new Rectangle(0, 0, Width, Height), 26)) ReplaceRegion(new Region(path));
            radialHover = new Point(125, 125);
            Invalidate();
        }

        private void ExitRadialMode()
        {
            if (!radialMode) return;
            radialMode = false;
            Bounds = compactBounds;
            ApplyCircleRegion();
            Invalidate();
        }

        private void PaintRadial(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Theme.Background);
            using (var border = new Pen(Theme.Border, 1.4f))
            using (var path = Theme.Rounded(new Rectangle(1, 1, Width - 3, Height - 3), 25))
                graphics.DrawPath(border, path);

            var zone = RadialZone(radialHover);
            var recommendation = radialActions != null && radialActions.Count > 0 ? radialActions[0].Title : "推荐动作";
            DrawRadialZone(graphics, new Rectangle(70, 12, 110, 52), "推荐", recommendation, zone == "recommend");
            DrawRadialZone(graphics, new Rectangle(10, 91, 82, 62), "暂存", "临时架", zone == "shelf");
            DrawRadialZone(graphics, new Rectangle(158, 91, 82, 62), "桌面", "复制副本", zone == "desktop");
            DrawRadialZone(graphics, new Rectangle(70, 186, 110, 52), "更多", "查看全部动作", zone == "more");

            var center = new Rectangle(89, 87, 72, 72);
            using (var gradient = new LinearGradientBrush(center, Theme.Blue, Theme.Violet, 45f)) graphics.FillEllipse(gradient, center);
            using (var pen = new Pen(Color.White, 2.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                graphics.DrawLine(pen, 125, 103, 125, 132);
                graphics.DrawLine(pen, 115, 122, 125, 132);
                graphics.DrawLine(pen, 135, 122, 125, 132);
            }
            using (var font = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(225, Color.White)))
            using (var format = Centered()) graphics.DrawString("DROP", font, brush, new Rectangle(94, 135, 62, 14), format);
        }

        private static void DrawRadialZone(Graphics graphics, Rectangle bounds, string title, string hint, bool active)
        {
            using (var path = Theme.Rounded(bounds, 12))
            using (var fill = new SolidBrush(active ? Color.FromArgb(41, 66, 93) : Theme.SurfaceHigh))
            using (var border = new Pen(active ? Theme.Cyan : Theme.Border, active ? 2f : 1f))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }
            using (var titleFont = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Bold))
            using (var hintFont = new Font("Microsoft YaHei UI", 6.8f))
            using (var titleBrush = new SolidBrush(active ? Theme.Cyan : Theme.Text))
            using (var hintBrush = new SolidBrush(Theme.Secondary))
            using (var format = Centered())
            {
                graphics.DrawString(title, titleFont, titleBrush, new Rectangle(bounds.X + 3, bounds.Y + 5, bounds.Width - 6, 21), format);
                var compactHint = hint.Length > 8 ? hint.Substring(0, 8) + "…" : hint;
                graphics.DrawString(compactHint, hintFont, hintBrush, new Rectangle(bounds.X + 3, bounds.Y + 27, bounds.Width - 6, 18), format);
            }
        }

        private static string RadialZone(Point point)
        {
            var dx = point.X - 125;
            var dy = point.Y - 125;
            if (Math.Abs(dx) < 45 && Math.Abs(dy) < 45) return "more";
            if (Math.Abs(dx) > Math.Abs(dy)) return dx < 0 ? "shelf" : "desktop";
            return dy < 0 ? "recommend" : "more";
        }

        private void ApplyCircleRegion()
        {
            using (var circle = new GraphicsPath())
            {
                circle.AddEllipse(0, 0, Width, Height);
                ReplaceRegion(new Region(circle));
            }
        }

        private void ReplaceRegion(Region value)
        {
            var previous = Region;
            Region = value;
            if (previous != null) previous.Dispose();
        }

        private void OnOrbMouseDown(object sender, MouseEventArgs args)
        {
            if (args.Button == MouseButtons.Middle)
            {
                ProcessClipboard();
                return;
            }
            if (args.Button != MouseButtons.Left) return;
            mouseDown = args.Location;
            moved = false;
        }

        private void OnOrbMouseMove(object sender, MouseEventArgs args)
        {
            if (args.Button != MouseButtons.Left) return;
            if (!moved && Math.Abs(args.X - mouseDown.X) + Math.Abs(args.Y - mouseDown.Y) < 5) return;
            moved = true;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
        }

        private void OnOrbMouseUp(object sender, MouseEventArgs args)
        {
            if (args.Button == MouseButtons.Left && !moved) ShowShelf();
            SnapToEdge();
            SaveLocation();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == ClipboardHotkeyId)
            {
                ProcessClipboard();
                return;
            }
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == CommandHotkeyId)
            {
                ShowQuickCommands();
                return;
            }
            base.WndProc(ref m);
            if (m.Msg == 0x0232)
            {
                SnapToEdge();
                SaveLocation();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            hotkeyRegistered = RegisterHotKey(Handle, ClipboardHotkeyId, ModControl | ModAlt, Keys.D);
            commandHotkeyRegistered = RegisterHotKey(Handle, CommandHotkeyId, ModControl | ModAlt, Keys.Space);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (hotkeyRegistered) UnregisterHotKey(Handle, ClipboardHotkeyId);
            if (commandHotkeyRegistered) UnregisterHotKey(Handle, CommandHotkeyId);
            hotkeyRegistered = false;
            commandHotkeyRegistered = false;
            base.OnHandleDestroyed(e);
        }

        private void ProcessClipboard()
        {
            try
            {
                DropItem item;
                var data = Clipboard.GetDataObject();
                if (data != null && (data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text)))
                {
                    item = DropItem.FromData(data);
                }
                else if (Clipboard.ContainsImage())
                {
                    var inbox = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DropOrb", "Inbox");
                    Directory.CreateDirectory(inbox);
                    var path = UniqueClipboardPath(inbox);
                    using (var image = Clipboard.GetImage()) image.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    var imageData = new DataObject();
                    imageData.SetData(DataFormats.FileDrop, new[] { path });
                    item = DropItem.FromData(imageData);
                }
                else
                {
                    Toast("剪贴板是空的", "先复制文件、图片、文字或链接。 ");
                    return;
                }
                ShowOrb();
                ShowActionPanel(item);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "DropOrb 无法读取剪贴板", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string UniqueClipboardPath(string folder)
        {
            var stem = "Clipboard-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(folder, stem + ".png");
            for (var index = 2; File.Exists(path); index++) path = Path.Combine(folder, stem + "-" + index + ".png");
            return path;
        }

        private static void OpenDownloads()
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static void CreateQuickNote()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var stem = Path.Combine(desktop, "随手记-" + DateTime.Now.ToString("MM-dd-HHmm"));
            var path = stem + ".txt";
            for (var index = 2; File.Exists(path); index++) path = stem + "-" + index + ".txt";
            File.WriteAllText(path, "", new System.Text.UTF8Encoding(false));
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static bool IsStartupEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                return key != null && key.GetValue("DropOrb") != null;
        }

        private static void SetStartup(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run"))
            {
                if (enabled) key.SetValue("DropOrb", "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue("DropOrb", false);
            }
        }

        private void SetSendTo(bool enabled)
        {
            try
            {
                SendToIntegration.SetInstalled(enabled);
                Toast(enabled ? "已加入文件管理器" : "已移除文件管理器入口",
                    enabled ? "选中文件后，右键“发送到”即可交给 DropOrb。" : "不会影响 DropOrb 的其他入口。");
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "DropOrb 无法更新“发送到”", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        internal void ProcessExternalArguments(string[] paths)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string[]>(ProcessExternalArguments), new object[] { paths });
                return;
            }
            if (paths == null || paths.Length == 0)
            {
                ShowOrb();
                return;
            }
            var existing = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Distinct(
                System.Linq.Enumerable.Where(paths, path => File.Exists(path) || Directory.Exists(path)), StringComparer.OrdinalIgnoreCase));
            if (existing.Length == 0)
            {
                Toast("没有可处理的内容", "所选文件可能已被移动或删除。");
                return;
            }
            try
            {
                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, existing);
                ShowOrb();
                ShowActionPanel(DropItem.FromData(data));
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "DropOrb 无法接收文件", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowQuickCommands()
        {
            if (quickCommandForm != null && !quickCommandForm.IsDisposed)
            {
                quickCommandForm.Activate();
                return;
            }
            var commands = new List<QuickCommand>
            {
                new QuickCommand("处理剪贴板", "识别刚复制的文件、图片、文字或链接", ProcessClipboard),
                new QuickCommand("打开下载文件夹", "直接进入常用下载目录", OpenDownloads),
                new QuickCommand("新建桌面便签", "创建并打开一个随手记文本", CreateQuickNote),
                new QuickCommand("打开临时架", "找回暂存的文件、文字和链接", ShowShelf),
                new QuickCommand("任务与撤销", "查看后台工作并撤销新生成内容", ShowActivityCenter),
                new QuickCommand("功能说明", "查看 DropOrb 的全部懒人用法", ShowHelp),
                new QuickCommand("隐藏投递球", "小球进入托盘，快捷键仍然可用", Hide)
            };
            quickCommandForm = new QuickCommandForm(commands);
            var work = Screen.FromPoint(Cursor.Position).WorkingArea;
            quickCommandForm.Location = new Point(work.Left + (work.Width - quickCommandForm.Width) / 2,
                work.Top + Math.Max(50, (work.Height - quickCommandForm.Height) / 3));
            quickCommandForm.FormClosed += delegate { quickCommandForm = null; };
            quickCommandForm.Show();
            quickCommandForm.Activate();
        }

        private void ShowShelf()
        {
            if (shelfForm != null && !shelfForm.IsDisposed)
            {
                shelfForm.Activate();
                return;
            }
            shelfForm = new ShelfForm(shelf, PanelAnchor());
            shelfForm.FormClosed += delegate { shelfForm = null; };
            shelfForm.Show();
            shelfForm.Activate();
        }

        private void ShowHelp()
        {
            if (helpForm != null && !helpForm.IsDisposed)
            {
                helpForm.Activate();
                return;
            }
            helpForm = new HelpForm(PanelAnchor());
            helpForm.FormClosed += delegate { helpForm = null; };
            helpForm.Show();
            helpForm.Activate();
        }

        private void ShowActivityCenter()
        {
            if (activityForm != null && !activityForm.IsDisposed)
            {
                activityForm.Activate();
                return;
            }
            activityForm = new ActivityCenterForm(jobs, undoStore, PanelAnchor());
            activityForm.FormClosed += delegate { activityForm = null; };
            activityForm.Show();
            activityForm.Activate();
        }

        private string ActivityTitle()
        {
            return jobs.RunningCount > 0 ? "任务与撤销  (" + jobs.RunningCount + " 进行中)" : "任务与撤销";
        }

        private void ResetPreferences()
        {
            if (MessageBox.Show(this, "清除已经学到的动作排序？", "重置动作偏好", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            engine.ResetPreferences();
            Toast("动作偏好已重置", "之后会重新学习你的选择。 ");
        }

        private void OnJobCompleted(object sender, JobCompletedEventArgs args)
        {
            if (args.Error != null)
            {
                Toast("后台任务失败", args.Job.Title + "：" + args.Error.Message);
                Invalidate();
                return;
            }
            if (args.Result != null && !string.IsNullOrWhiteSpace(args.Result.ClipboardText)) Clipboard.SetText(args.Result.ClipboardText);
            Toast(args.Job.Title + "完成", string.IsNullOrWhiteSpace(args.Job.Message) ? "已处理完成，可在任务中心查看。" : args.Job.Message);
            Invalidate();
        }

        private void ShowOrb()
        {
            Show();
            TopMost = true;
            Activate();
        }

        private Point PanelAnchor()
        {
            var work = Screen.FromControl(this).WorkingArea;
            var openLeft = Left > work.Left + work.Width / 2;
            return new Point(openLeft ? Left - 350 : Right + 10, Math.Min(Top, work.Bottom - 430));
        }

        private void SnapToEdge()
        {
            var work = Screen.FromControl(this).WorkingArea;
            var center = Left + Width / 2;
            var x = center < work.Left + work.Width / 2 ? work.Left + 12 : work.Right - Width - 12;
            var y = Math.Max(work.Top + 12, Math.Min(Top, work.Bottom - Height - 12));
            Location = new Point(x, y);
        }

        private Point LoadLocation()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    var value = new JavaScriptSerializer().Deserialize<DictionarySetting>(File.ReadAllText(settingsPath));
                    var point = new Point(value.X, value.Y);
                    if (Screen.FromPoint(point).WorkingArea.Contains(point)) return point;
                }
            }
            catch { }
            var work = Screen.PrimaryScreen.WorkingArea;
            return new Point(work.Right - Width - 18, work.Top + work.Height / 3);
        }

        private void SaveLocation()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(settingsPath, new JavaScriptSerializer().Serialize(new DictionarySetting { X = Left, Y = Top }));
            }
            catch { }
        }

        private void Toast(string title, string message)
        {
            trayIcon.BalloonTipTitle = title;
            trayIcon.BalloonTipText = message;
            trayIcon.ShowBalloonTip(1800);
        }

        private void OnFormClosing(object sender, FormClosingEventArgs args)
        {
            if (allowExit) return;
            args.Cancel = true;
            Hide();
        }

        private void OnFormClosed(object sender, FormClosedEventArgs args)
        {
            SaveLocation();
            animationTimer.Stop();
            animationTimer.Dispose();
            radialLeaveTimer.Stop();
            radialLeaveTimer.Dispose();
            jobs.Completed -= OnJobCompleted;
            if (quickCommandForm != null && !quickCommandForm.IsDisposed) quickCommandForm.Close();
            trayIcon.Visible = false;
            trayIcon.Dispose();
        }

        private static StringFormat Centered()
        {
            return new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        }

        private sealed class DictionarySetting
        {
            public int X { get; set; }
            public int Y { get; set; }
        }
    }
}
