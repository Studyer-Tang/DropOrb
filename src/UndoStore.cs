using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace DropOrb
{
    internal sealed class UndoStore
    {
        private readonly string path;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private readonly object gate = new object();
        private List<UndoEntry> entries;

        public UndoStore(string dataDirectory)
        {
            path = Path.Combine(dataDirectory, "undo.json");
            entries = Load();
        }

        public IList<UndoEntry> Entries { get { lock (gate) return entries.ToList().AsReadOnly(); } }
        public event EventHandler Changed;

        public void Record(string title, IEnumerable<string> paths)
        {
            var existing = paths.Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(Path.GetFullPath)
                .Where(value => File.Exists(value) || Directory.Exists(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (existing.Count == 0) return;
            lock (gate)
            {
                entries.Insert(0, new UndoEntry { Title = title, Paths = existing, CreatedAt = DateTime.Now });
                if (entries.Count > 24) entries.RemoveRange(24, entries.Count - 24);
                Save();
            }
            RaiseChanged();
        }

        public void Undo(UndoEntry entry)
        {
            lock (gate)
            {
                foreach (var value in entry.Paths)
                {
                    var full = Path.GetFullPath(value);
                    var root = Path.GetPathRoot(full);
                    if (string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("拒绝撤销磁盘根目录。 ");
                    if (File.Exists(full)) File.Delete(full);
                    else if (Directory.Exists(full)) Directory.Delete(full, true);
                }
                entries.Remove(entry);
                Save();
            }
            RaiseChanged();
        }

        private List<UndoEntry> Load()
        {
            try
            {
                if (File.Exists(path))
                {
                    var loaded = serializer.Deserialize<List<UndoEntry>>(File.ReadAllText(path, Encoding.UTF8)) ?? new List<UndoEntry>();
                    return loaded.Where(entry => entry.Paths != null && entry.Paths.Any(value => File.Exists(value) || Directory.Exists(value))).ToList();
                }
            }
            catch { }
            return new List<UndoEntry>();
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, serializer.Serialize(entries), new UTF8Encoding(false));
        }

        private void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }

    internal sealed class UndoEntry
    {
        public string Title { get; set; }
        public List<string> Paths { get; set; }
        public DateTime CreatedAt { get; set; }

        public override string ToString()
        {
            var count = Paths == null ? 0 : Paths.Count;
            return CreatedAt.ToString("MM-dd HH:mm") + "   " + Title + "   (" + count + ")";
        }
    }
}
