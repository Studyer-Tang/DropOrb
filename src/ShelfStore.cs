using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace DropOrb
{
    internal sealed class ShelfStore
    {
        private readonly string dataDirectory;
        private readonly string shelfPath;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private List<ShelfEntry> entries;

        public ShelfStore()
        {
            dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DropOrb");
            shelfPath = Path.Combine(dataDirectory, "shelf.json");
            entries = Load();
        }

        public IList<ShelfEntry> Entries
        {
            get { return entries.AsReadOnly(); }
        }

        public void Add(DropItem item)
        {
            if (item.Paths.Count > 0)
            {
                foreach (var path in item.Paths)
                {
                    AddEntry(new ShelfEntry
                    {
                        Kind = Directory.Exists(path) ? "folder" : "file",
                        Value = path,
                        Label = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),
                        AddedAt = DateTime.Now
                    });
                }
            }
            else
            {
                AddEntry(new ShelfEntry
                {
                    Kind = item.Kind == DropKind.Url ? "url" : "text",
                    Value = item.Text,
                    Label = item.DisplayName,
                    AddedAt = DateTime.Now
                });
            }
            Save();
        }

        public void Remove(ShelfEntry entry)
        {
            entries.Remove(entry);
            Save();
        }

        private void AddEntry(ShelfEntry entry)
        {
            entries.RemoveAll(existing => string.Equals(existing.Kind, entry.Kind, StringComparison.OrdinalIgnoreCase) && string.Equals(existing.Value, entry.Value, StringComparison.OrdinalIgnoreCase));
            entries.Insert(0, entry);
            if (entries.Count > 40) entries.RemoveRange(40, entries.Count - 40);
        }

        private List<ShelfEntry> Load()
        {
            try
            {
                if (!File.Exists(shelfPath)) return new List<ShelfEntry>();
                return serializer.Deserialize<List<ShelfEntry>>(File.ReadAllText(shelfPath, Encoding.UTF8)) ?? new List<ShelfEntry>();
            }
            catch
            {
                return new List<ShelfEntry>();
            }
        }

        private void Save()
        {
            Directory.CreateDirectory(dataDirectory);
            var temporary = shelfPath + ".tmp";
            File.WriteAllText(temporary, serializer.Serialize(entries), new UTF8Encoding(false));
            if (File.Exists(shelfPath)) File.Replace(temporary, shelfPath, null);
            else File.Move(temporary, shelfPath);
        }
    }
}
