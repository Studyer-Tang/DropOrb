using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DropOrb
{
    internal sealed class ActionEngine
    {
        private readonly ShelfStore shelf;
        private readonly PreferenceStore preferences;

        public ActionEngine(ShelfStore shelfStore, PreferenceStore preferenceStore)
        {
            shelf = shelfStore;
            preferences = preferenceStore;
        }

        public List<ActionSpec> GetActions(DropItem item)
        {
            List<ActionSpec> actions;
            switch (item.Kind)
            {
                case DropKind.Image: actions = ImageActions(); break;
                case DropKind.Archive: actions = ArchiveActions(); break;
                case DropKind.Folder: actions = FolderActions(); break;
                case DropKind.Url: actions = UrlActions(); break;
                case DropKind.Text: actions = TextActions(); break;
                case DropKind.Pdf: actions = PdfActions(); break;
                case DropKind.Files: actions = MultipleActions(); break;
                default: actions = GenericActions(); break;
            }
            return preferences.Order(item.Kind, actions);
        }

        public void Remember(DropKind kind, ActionSpec action) { preferences.Remember(kind, action); }
        public void ResetPreferences() { preferences.Reset(); }

        private List<ActionSpec> ImageActions()
        {
            return new List<ActionSpec>
            {
                Spec("置顶预览", "把图片钉在桌面上", (item, owner) => PinImages(item)),
                BackgroundSpec("缩小到 50%", "在后台生成尺寸减半副本", ResizeImagesWork),
                BackgroundSpec("转换格式", "在后台完成 PNG/JPG 互转", ConvertImagesWork),
                BackgroundSpec("压缩副本", "在后台生成较小 JPG", CompressImagesWork),
                Spec("复制图片", "复制第一张到剪贴板", (item, owner) => CopyImage(item)),
                Spec("打开位置", "在资源管理器中定位", (item, owner) => OpenContaining(item.PrimaryPath))
            };
        }

        private List<ActionSpec> ArchiveActions()
        {
            return new List<ActionSpec>
            {
                Spec("解压并打开", "安全解压到同名文件夹", (item, owner) => ExtractZip(item.PrimaryPath)),
                Spec("预览内容", "先看看压缩包里有什么", (item, owner) => PreviewZip(item.PrimaryPath, owner)),
                BackgroundSpec("复制到桌面", "后台生成一份不覆盖的副本", CopyToDesktopWork),
                Spec("计算 SHA-256", "校验下载文件是否完整", (item, owner) => CopyHashes(item, owner)),
                ShelfSpec(),
                Spec("打开位置", "在资源管理器中定位", (item, owner) => OpenContaining(item.PrimaryPath))
            };
        }

        private List<ActionSpec> FolderActions()
        {
            return new List<ActionSpec>
            {
                BackgroundSpec("打包 ZIP", "在后台生成新的压缩包", item => ZipFolderWork(item.PrimaryPath)),
                Spec("生成目录树", "输出可复制的 TXT 目录", (item, owner) => GenerateTree(item.PrimaryPath)),
                Spec("复制文件名", "复制第一层所有名称", (item, owner) => CopyNames(item.PrimaryPath)),
                Spec("复制完整路径", "直接放进剪贴板", (item, owner) => Clipboard.SetText(item.PrimaryPath)),
                BackgroundSpec("复制到桌面", "后台生成一份不覆盖的副本", CopyToDesktopWork),
                ShelfSpec()
            };
        }

        private List<ActionSpec> UrlActions()
        {
            return new List<ActionSpec>
            {
                Spec("打开链接", "使用默认浏览器", (item, owner) => OpenUrl(item.Text)),
                Spec("净化并复制", "去掉常见跟踪参数", (item, owner) => Clipboard.SetText(CleanUrl(item.Text))),
                Spec("复制链接", "复制到剪贴板", (item, owner) => Clipboard.SetText(item.Text)),
                ShelfSpec(),
                Spec("保存为 .url", "在桌面生成快捷链接", (item, owner) => SaveUrlShortcut(item.Text))
            };
        }

        private List<ActionSpec> TextActions()
        {
            return new List<ActionSpec>
            {
                Spec("复制纯文本", "移除首尾空白后复制", (item, owner) => Clipboard.SetText(item.Text.Trim())),
                Spec("整理成一行", "合并空白和换行", (item, owner) => Clipboard.SetText(ToSingleLine(item.Text))),
                Spec("提取所有链接", "从文字中找出网址", (item, owner) => ExtractLinks(item.Text, owner)),
                ShelfSpec(),
                Spec("保存为 TXT", "在桌面生成文本文件", (item, owner) => SaveText(item.Text)),
                Spec("统计文本", "字符、单词与行数", (item, owner) => ShowTextStats(item.Text, owner))
            };
        }

        private List<ActionSpec> PdfActions()
        {
            return new List<ActionSpec>
            {
                Spec("打开 PDF", "使用默认阅读器", (item, owner) => OpenPath(item.PrimaryPath)),
                BackgroundSpec("复制到桌面", "后台生成一份不覆盖的副本", CopyToDesktopWork),
                Spec("计算 SHA-256", "复制文件校验值", (item, owner) => CopyHashes(item, owner)),
                ShelfSpec(),
                Spec("复制路径", "复制完整文件路径", (item, owner) => Clipboard.SetText(item.PrimaryPath)),
                Spec("打开位置", "在资源管理器中定位", (item, owner) => OpenContaining(item.PrimaryPath))
            };
        }

        private List<ActionSpec> GenericActions()
        {
            return new List<ActionSpec>
            {
                Spec("打开", "使用默认程序打开", (item, owner) => OpenPath(item.PrimaryPath)),
                BackgroundSpec("复制到桌面", "后台生成一份不覆盖的副本", CopyToDesktopWork),
                BackgroundSpec("打包 ZIP", "在后台生成压缩副本", item => ZipSingleFileWork(item.PrimaryPath)),
                Spec("计算 SHA-256", "复制文件校验值", (item, owner) => CopyHashes(item, owner)),
                ShelfSpec(),
                Spec("打开位置", "在资源管理器中定位", (item, owner) => OpenContaining(item.PrimaryPath))
            };
        }

        private List<ActionSpec> MultipleActions()
        {
            return new List<ActionSpec>
            {
                BackgroundSpec("打包为一个 ZIP", "在后台把文件和文件夹收好", ZipItemsWork),
                Spec("复制所有名称", "只复制文件名", (item, owner) => Clipboard.SetText(string.Join(Environment.NewLine, item.Paths.Select(path => Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))).ToArray()))),
                Spec("复制所有路径", "每行一个完整路径", (item, owner) => Clipboard.SetText(string.Join(Environment.NewLine, item.Paths.ToArray()))),
                BackgroundSpec("全部复制到桌面", "后台复制并自动避开同名", CopyToDesktopWork),
                ShelfSpec(),
                Spec("打开第一个位置", "在资源管理器中定位", (item, owner) => OpenContaining(item.PrimaryPath))
            };
        }

        private ActionSpec ShelfSpec()
        {
            return Spec("加入临时架", "以后再处理，不用分类", (item, owner) =>
            {
                shelf.Add(item);
                MessageBox.Show(owner, "已经放进临时架。原文件没有移动。", "DropOrb", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        private static ActionSpec Spec(string title, string hint, Action<DropItem, IWin32Window> action)
        {
            return new ActionSpec { Id = title, Title = title, Hint = hint, Execute = action };
        }

        private static ActionSpec BackgroundSpec(string title, string hint, Func<DropItem, ActionResult> action)
        {
            return new ActionSpec { Id = title, Title = title, Hint = hint, BackgroundExecute = action };
        }

        private static void PinImages(DropItem item)
        {
            foreach (var path in item.Paths.Where(File.Exists).Take(6)) new PinnedImageForm(path).Show();
        }

        private static ActionResult ResizeImagesWork(DropItem item)
        {
            var outputs = new List<string>();
            foreach (var source in item.Paths.Where(File.Exists))
            {
                using (var image = Image.FromFile(source))
                using (var resized = new Bitmap(Math.Max(1, image.Width / 2), Math.Max(1, image.Height / 2)))
                {
                    using (var graphics = Graphics.FromImage(resized))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(image, new Rectangle(0, 0, resized.Width, resized.Height));
                    }
                    var jpeg = Path.GetExtension(source).Equals(".jpg", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(source).Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
                    var target = UniquePath(Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + "-50pct" + (jpeg ? ".jpg" : ".png")));
                    if (jpeg) SaveJpeg(resized, target, 88L);
                    else resized.Save(target, ImageFormat.Png);
                    outputs.Add(target);
                }
            }
            return new ActionResult { Message = "已生成 " + outputs.Count + " 张图片", Outputs = outputs };
        }

        private static void CopyImage(DropItem item)
        {
            using (var image = Image.FromFile(item.PrimaryPath)) Clipboard.SetImage(new Bitmap(image));
        }

        private static ActionResult ConvertImagesWork(DropItem item)
        {
            var outputs = new List<string>();
            foreach (var source in item.Paths.Where(File.Exists))
            {
                var toJpeg = Path.GetExtension(source).Equals(".png", StringComparison.OrdinalIgnoreCase);
                var target = UniquePath(Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + (toJpeg ? "-converted.jpg" : "-converted.png")));
                using (var image = Image.FromFile(source))
                {
                    if (toJpeg) SaveJpeg(image, target, 90L);
                    else image.Save(target, ImageFormat.Png);
                }
                outputs.Add(target);
            }
            return new ActionResult { Message = "已转换 " + outputs.Count + " 张图片", Outputs = outputs };
        }

        private static ActionResult CompressImagesWork(DropItem item)
        {
            var outputs = new List<string>();
            foreach (var source in item.Paths.Where(File.Exists))
            {
                var target = UniquePath(Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + "-compressed.jpg"));
                using (var image = Image.FromFile(source)) SaveJpeg(image, target, 72L);
                outputs.Add(target);
            }
            return new ActionResult { Message = "已压缩 " + outputs.Count + " 张图片", Outputs = outputs };
        }

        private static void SaveJpeg(Image image, string target, long quality)
        {
            var codec = ImageCodecInfo.GetImageEncoders().First(item => item.FormatID == ImageFormat.Jpeg.Guid);
            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                image.Save(target, codec, parameters);
            }
        }

        private static void ExtractZip(string source)
        {
            var destination = UniqueDirectory(Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source)));
            Directory.CreateDirectory(destination);
            var root = Path.GetFullPath(destination + Path.DirectorySeparatorChar);
            using (var archive = ZipFile.OpenRead(source))
            {
                foreach (var entry in archive.Entries)
                {
                    var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("压缩包包含不安全的路径，已停止解压。 ");
                    if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(target);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        entry.ExtractToFile(target, false);
                    }
                }
            }
            OpenPath(destination);
        }

        private static void PreviewZip(string source, IWin32Window owner)
        {
            using (var archive = ZipFile.OpenRead(source))
            {
                var names = archive.Entries.Take(18).Select(entry => "• " + entry.FullName).ToList();
                var more = archive.Entries.Count > names.Count ? Environment.NewLine + "…另有 " + (archive.Entries.Count - names.Count) + " 项" : "";
                MessageBox.Show(owner, string.Join(Environment.NewLine, names.ToArray()) + more, "压缩包内容", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static ActionResult ZipFolderWork(string folder)
        {
            var target = UniquePath(folder.TrimEnd(Path.DirectorySeparatorChar) + ".zip");
            ZipFile.CreateFromDirectory(folder, target, CompressionLevel.Optimal, false);
            return Result("压缩完成", target);
        }

        private static ActionResult ZipSingleFileWork(string source)
        {
            var target = UniquePath(Path.Combine(Path.GetDirectoryName(source), Path.GetFileNameWithoutExtension(source) + ".zip"));
            using (var archive = ZipFile.Open(target, ZipArchiveMode.Create)) archive.CreateEntryFromFile(source, Path.GetFileName(source), CompressionLevel.Optimal);
            return Result("压缩完成", target);
        }

        private static ActionResult ZipItemsWork(DropItem item)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var target = UniquePath(Path.Combine(desktop, "DropOrb-bundle-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".zip"));
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var archive = ZipFile.Open(target, ZipArchiveMode.Create))
            {
                foreach (var path in item.Paths)
                {
                    if (File.Exists(path)) AddFileToArchive(archive, path, Path.GetFileName(path), used);
                    else if (Directory.Exists(path)) AddDirectoryToArchive(archive, path, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), used);
                }
            }
            return Result("压缩完成", target);
        }

        private static void AddDirectoryToArchive(ZipArchive archive, string folder, string prefix, HashSet<string> used)
        {
            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(folder);
                directories = Directory.GetDirectories(folder);
            }
            catch { return; }
            foreach (var file in files) AddFileToArchive(archive, file, prefix + "/" + Path.GetFileName(file), used);
            foreach (var directory in directories) AddDirectoryToArchive(archive, directory, prefix + "/" + Path.GetFileName(directory), used);
        }

        private static void AddFileToArchive(ZipArchive archive, string source, string entryName, HashSet<string> used)
        {
            entryName = entryName.Replace('\\', '/');
            var candidate = entryName;
            for (var index = 2; !used.Add(candidate); index++)
            {
                var extension = Path.GetExtension(entryName);
                candidate = entryName.Substring(0, entryName.Length - extension.Length) + "-" + index + extension;
            }
            archive.CreateEntryFromFile(source, candidate, CompressionLevel.Optimal);
        }

        private static ActionResult CopyToDesktopWork(DropItem item)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var outputs = new List<string>();
            foreach (var source in item.Paths)
            {
                var name = Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));
                var target = UniquePath(Path.Combine(desktop, name));
                if (Directory.Exists(source)) CopyDirectory(source, target);
                else if (File.Exists(source)) File.Copy(source, target, false);
                else continue;
                outputs.Add(target);
            }
            return new ActionResult { Message = "已复制 " + outputs.Count + " 项", Outputs = outputs };
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
            foreach (var folder in Directory.GetDirectories(source)) CopyDirectory(folder, Path.Combine(destination, Path.GetFileName(folder)));
        }

        private static void CopyHashes(DropItem item, IWin32Window owner)
        {
            var lines = new List<string>();
            using (var algorithm = SHA256.Create())
            {
                foreach (var path in item.Paths.Where(File.Exists))
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        lines.Add(BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "") + "  " + Path.GetFileName(path));
                }
            }
            if (lines.Count == 0) throw new InvalidOperationException("没有可以计算校验值的文件。 ");
            Clipboard.SetText(string.Join(Environment.NewLine, lines.ToArray()));
            MessageBox.Show(owner, "SHA-256 已复制到剪贴板。", "DropOrb", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void GenerateTree(string folder)
        {
            var output = UniquePath(Path.Combine(Path.GetDirectoryName(folder), Path.GetFileName(folder) + "-tree.txt"));
            var builder = new StringBuilder();
            builder.AppendLine(Path.GetFileName(folder) + "/");
            AppendTree(folder, builder, "", 0, new int[] { 0 });
            File.WriteAllText(output, builder.ToString(), new UTF8Encoding(false));
            OpenPath(output);
        }

        private static void AppendTree(string folder, StringBuilder builder, string indent, int depth, int[] count)
        {
            if (depth > 8 || count[0] > 5000) return;
            IEnumerable<string> children;
            try { children = Directory.EnumerateFileSystemEntries(folder).OrderBy(path => path).Take(1000).ToArray(); }
            catch { return; }
            foreach (var child in children)
            {
                count[0]++;
                var directory = Directory.Exists(child);
                builder.AppendLine(indent + "├─ " + Path.GetFileName(child) + (directory ? "/" : ""));
                if (directory) AppendTree(child, builder, indent + "│  ", depth + 1, count);
                if (count[0] > 5000) { builder.AppendLine(indent + "└─ …已达到 5000 项限制"); break; }
            }
        }

        private static void CopyNames(string folder)
        {
            var names = Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName).ToArray();
            Clipboard.SetText(string.Join(Environment.NewLine, names));
        }

        private static void SaveUrlShortcut(string url)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var host = new Uri(url).Host.Replace(".", "-");
            var target = UniquePath(Path.Combine(desktop, host + ".url"));
            File.WriteAllText(target, "[InternetShortcut]" + Environment.NewLine + "URL=" + url, new UTF8Encoding(false));
            OpenContaining(target);
        }

        private static string CleanUrl(string url)
        {
            var builder = new UriBuilder(url);
            var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "fbclid", "gclid", "dclid", "msclkid", "igshid", "mkt_tok", "mc_cid", "mc_eid", "spm", "ref", "ref_src"
            };
            var kept = new List<string>();
            foreach (var pair in builder.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = pair.IndexOf('=');
                var rawKey = separator < 0 ? pair : pair.Substring(0, separator);
                var key = Uri.UnescapeDataString(rawKey.Replace("+", " "));
                if (key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) || blocked.Contains(key)) continue;
                kept.Add(pair);
            }
            builder.Query = string.Join("&", kept.ToArray());
            return builder.Uri.AbsoluteUri;
        }

        private static string ToSingleLine(string text)
        {
            return Regex.Replace(text.Trim(), "\\s+", " ");
        }

        private static void ExtractLinks(string text, IWin32Window owner)
        {
            var links = Regex.Matches(text, "https?://[^\\s<>\\\"']+", RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Select(match => match.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (links.Length == 0) throw new InvalidOperationException("这段文字里没有识别到网页链接。 ");
            Clipboard.SetText(string.Join(Environment.NewLine, links));
            MessageBox.Show(owner, "已提取并复制 " + links.Length + " 个链接。", "DropOrb", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void SaveText(string text)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var target = UniquePath(Path.Combine(desktop, "DropOrb-note.txt"));
            File.WriteAllText(target, text, new UTF8Encoding(false));
            OpenPath(target);
        }

        private static void ShowTextStats(string text, IWin32Window owner)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n').Length;
            var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            MessageBox.Show(owner, "字符：" + text.Length + Environment.NewLine + "词语：" + words + Environment.NewLine + "行数：" + lines, "文本统计", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void OpenUrl(string url) { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        private static void OpenPath(string path) { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        private static void OpenContaining(string path)
        {
            if (Directory.Exists(path)) OpenPath(path);
            else Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }

        private static string UniquePath(string desired)
        {
            if (!File.Exists(desired) && !Directory.Exists(desired)) return desired;
            var folder = Path.GetDirectoryName(desired);
            var name = Path.GetFileNameWithoutExtension(desired);
            var extension = Path.GetExtension(desired);
            for (var index = 2; index < 10000; index++)
            {
                var candidate = Path.Combine(folder, name + "-" + index + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }
            throw new IOException("无法创建不重复的输出文件名。 ");
        }

        private static string UniqueDirectory(string desired)
        {
            return UniquePath(desired);
        }

        private static ActionResult Result(string message, params string[] outputs)
        {
            return new ActionResult { Message = message, Outputs = outputs.ToList() };
        }
    }
}
