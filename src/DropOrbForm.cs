using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DropOrb
{
    internal sealed class DropOrbForm : Form
    {
        private readonly ShelfStore shelf;
        private readonly ActionEngine engine;
        private readonly NotifyIcon trayIcon;
        private readonly string settingsPath;
        private ShelfForm shelfForm;
        private HelpForm helpForm;
        private bool dragHover;
        private bool allowExit;
        private Point mouseDown;
        private bool moved;
        private bool hotkeyRegistered;

        private const int HotkeyId = 0xD09;
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
            shelf = new ShelfStore();
            engine = new ActionEngine(shelf);
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DropOrb", "settings.json");
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
            using (var circle = new GraphicsPath())
            {
                circle.AddEllipse(0, 0, Width, Height);
                Region = new Region(circle);
            }
            Location = LoadLocation();

            var context = new ContextMenuStrip();
            var helpItem = context.Items.Add("功能说明");
            var clipboardItem = context.Items.Add("处理剪贴板  Ctrl+Alt+D");
            var shelfItem = context.Items.Add("打开临时架");
            var downloadsItem = context.Items.Add("打开下载文件夹");
            var noteItem = context.Items.Add("新建桌面便签");
            var startupItem = new ToolStripMenuItem("开机自动启动") { CheckOnClick = true };
            context.Items.Add(startupItem);
            var hideItem = context.Items.Add("隐藏投递球");
            context.Items.Add(new ToolStripSeparator());
            var exitItem = context.Items.Add("退出 DropOrb");
            helpItem.Click += delegate { ShowHelp(); };
            clipboardItem.Click += delegate { ProcessClipboard(); };
            shelfItem.Click += delegate { ShowShelf(); };
            downloadsItem.Click += delegate { OpenDownloads(); };
            noteItem.Click += delegate { CreateQuickNote(); };
            startupItem.Click += delegate { SetStartup(startupItem.Checked); };
            hideItem.Click += delegate { Hide(); };
            exitItem.Click += delegate { allowExit = true; Close(); };
            context.Opening += delegate { startupItem.Checked = IsStartupEnabled(); };
            ContextMenuStrip = context;

            var trayMenu = new ContextMenuStrip();
            var showTray = trayMenu.Items.Add("显示投递球");
            var helpTray = trayMenu.Items.Add("功能说明");
            var clipboardTray = trayMenu.Items.Add("处理剪贴板  Ctrl+Alt+D");
            var shelfTray = trayMenu.Items.Add("临时架");
            var downloadsTray = trayMenu.Items.Add("打开下载文件夹");
            var noteTray = trayMenu.Items.Add("新建桌面便签");
            var startupTray = new ToolStripMenuItem("开机自动启动") { CheckOnClick = true };
            trayMenu.Items.Add(startupTray);
            trayMenu.Items.Add(new ToolStripSeparator());
            var exitTray = trayMenu.Items.Add("退出");
            showTray.Click += delegate { ShowOrb(); };
            helpTray.Click += delegate { ShowHelp(); };
            clipboardTray.Click += delegate { ProcessClipboard(); };
            shelfTray.Click += delegate { ShowShelf(); };
            downloadsTray.Click += delegate { OpenDownloads(); };
            noteTray.Click += delegate { CreateQuickNote(); };
            startupTray.Click += delegate { SetStartup(startupTray.Checked); };
            exitTray.Click += delegate { allowExit = true; Close(); };
            trayMenu.Opening += delegate { startupTray.Checked = IsStartupEnabled(); };
            trayIcon = new NotifyIcon { Icon = SystemIcons.Application, Text = "DropOrb · 拖进来，马上处理", ContextMenuStrip = trayMenu, Visible = true };
            trayIcon.DoubleClick += delegate { ShowOrb(); };

            DragEnter += OnDragEnter;
            DragLeave += delegate { dragHover = false; Invalidate(); };
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
                if (Program.InspectHelp) ShowHelp();
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
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var outer = new Rectangle(4, 4, 70, 70);
            using (var shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(shadow, 5, 7, 68, 68);
            using (var gradient = new LinearGradientBrush(outer, dragHover ? Theme.Cyan : Theme.Blue, Theme.Violet, 45f)) g.FillEllipse(gradient, outer);
            using (var glow = new Pen(Color.FromArgb(dragHover ? 230 : 95, Color.White), dragHover ? 3f : 1.5f)) g.DrawEllipse(glow, outer);
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
                DropItem.FromData(args.Data);
                args.Effect = DragDropEffects.Copy;
                dragHover = true;
                Invalidate();
            }
            catch
            {
                args.Effect = DragDropEffects.None;
            }
        }

        private void OnDragDrop(object sender, DragEventArgs args)
        {
            dragHover = false;
            Invalidate();
            try
            {
                var item = DropItem.FromData(args.Data);
                var modifiers = Control.ModifierKeys;
                if ((modifiers & Keys.Control) == Keys.Control)
                {
                    shelf.Add(item);
                    Toast("已加入临时架", "原文件没有移动。 ");
                    return;
                }
                var actions = engine.GetActions(item);
                if ((modifiers & Keys.Shift) == Keys.Shift && actions.Count > 0)
                {
                    actions[0].Execute(item, this);
                    return;
                }
                ShowActionPanel(item, actions);
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "DropOrb 无法处理", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowActionPanel(DropItem item)
        {
            ShowActionPanel(item, engine.GetActions(item));
        }

        private void ShowActionPanel(DropItem item, System.Collections.Generic.IList<ActionSpec> actions)
        {
            var panel = new ActionPanelForm(item, actions, PanelAnchor());
            panel.Show();
            panel.Activate();
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
            if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId)
            {
                ProcessClipboard();
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
            hotkeyRegistered = RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt, Keys.D);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (hotkeyRegistered) UnregisterHotKey(Handle, HotkeyId);
            hotkeyRegistered = false;
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
