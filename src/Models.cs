using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DropOrb
{
    internal enum DropKind
    {
        Image,
        Archive,
        Folder,
        Pdf,
        Url,
        Text,
        File,
        Files
    }

    internal sealed class DropItem
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
        };

        public DropKind Kind { get; private set; }
        public List<string> Paths { get; private set; }
        public string Text { get; private set; }
        public string DisplayName { get; private set; }

        private DropItem()
        {
            Paths = new List<string>();
            Text = "";
            DisplayName = "";
        }

        public static DropItem FromData(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = ((string[])data.GetData(DataFormats.FileDrop) ?? new string[0])
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .ToList();
                if (paths.Count == 0) throw new InvalidDataException("没有识别到可处理的文件。 ");
                var item = new DropItem { Paths = paths };
                if (paths.Count > 1)
                {
                    var allImages = paths.All(path => File.Exists(path) && ImageExtensions.Contains(Path.GetExtension(path)));
                    item.Kind = allImages ? DropKind.Image : DropKind.Files;
                    item.DisplayName = paths.Count + " 个项目";
                    return item;
                }
                var primary = paths[0];
                item.Kind = ClassifyPath(primary);
                item.DisplayName = Path.GetFileName(primary.TrimEnd(Path.DirectorySeparatorChar));
                return item;
            }

            var raw = Convert.ToString(data.GetData(DataFormats.UnicodeText) ?? data.GetData(DataFormats.Text) ?? "").Trim();
            if (raw.Length == 0) throw new InvalidDataException("没有识别到文字或文件。 ");
            Uri uri;
            var isUrl = Uri.TryCreate(raw, UriKind.Absolute, out uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            return new DropItem
            {
                Kind = isUrl ? DropKind.Url : DropKind.Text,
                Text = raw,
                DisplayName = isUrl ? uri.Host : Preview(raw, 42)
            };
        }

        public static DropKind ClassifyPath(string path)
        {
            if (Directory.Exists(path)) return DropKind.Folder;
            var extension = Path.GetExtension(path);
            if (ImageExtensions.Contains(extension)) return DropKind.Image;
            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)) return DropKind.Archive;
            if (extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)) return DropKind.Pdf;
            return DropKind.File;
        }

        public string PrimaryPath
        {
            get { return Paths.Count == 0 ? "" : Paths[0]; }
        }

        public string KindLabel
        {
            get
            {
                switch (Kind)
                {
                    case DropKind.Image: return Paths.Count > 1 ? "图片组" : "图片";
                    case DropKind.Archive: return "ZIP 压缩包";
                    case DropKind.Folder: return "文件夹";
                    case DropKind.Pdf: return "PDF 文档";
                    case DropKind.Url: return "网页链接";
                    case DropKind.Text: return "文字";
                    case DropKind.Files: return "多个文件";
                    default: return "文件";
                }
            }
        }

        private static string Preview(string value, int length)
        {
            var singleLine = value.Replace("\r", " ").Replace("\n", " ");
            return singleLine.Length <= length ? singleLine : singleLine.Substring(0, length) + "…";
        }
    }

    internal sealed class ActionSpec
    {
        public string Title { get; set; }
        public string Hint { get; set; }
        public Action<DropItem, IWin32Window> Execute { get; set; }
    }

    internal sealed class ShelfEntry
    {
        public string Kind { get; set; }
        public string Value { get; set; }
        public string Label { get; set; }
        public DateTime AddedAt { get; set; }

        public override string ToString()
        {
            return AddedAt.ToString("MM-dd HH:mm") + "   " + Label;
        }
    }
}
