using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace DropOrb
{
    internal sealed class PreferenceStore
    {
        private readonly string path;
        private readonly JavaScriptSerializer serializer = new JavaScriptSerializer();
        private Dictionary<string, Dictionary<string, int>> scores;

        public PreferenceStore(string dataDirectory)
        {
            path = Path.Combine(dataDirectory, "preferences.json");
            scores = Load();
        }

        public List<ActionSpec> Order(DropKind kind, IEnumerable<ActionSpec> actions)
        {
            Dictionary<string, int> kindScores;
            scores.TryGetValue(kind.ToString(), out kindScores);
            return actions.Select((action, index) => new { Action = action, Index = index, Score = Score(kindScores, action.Id) })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Index)
                .Select(item => item.Action)
                .ToList();
        }

        public void Remember(DropKind kind, ActionSpec action)
        {
            var key = kind.ToString();
            Dictionary<string, int> kindScores;
            if (!scores.TryGetValue(key, out kindScores))
            {
                kindScores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                scores[key] = kindScores;
            }
            int value;
            kindScores.TryGetValue(action.Id, out value);
            kindScores[action.Id] = Math.Min(1000, value + 1);
            Save();
        }

        public void Reset()
        {
            scores.Clear();
            Save();
        }

        private static int Score(IDictionary<string, int> values, string id)
        {
            int value;
            return values != null && values.TryGetValue(id, out value) ? value : 0;
        }

        private Dictionary<string, Dictionary<string, int>> Load()
        {
            try
            {
                if (File.Exists(path))
                    return serializer.Deserialize<Dictionary<string, Dictionary<string, int>>>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch { }
            return new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, serializer.Serialize(scores), new UTF8Encoding(false));
        }
    }
}
