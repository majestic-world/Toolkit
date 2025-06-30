using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace L2Toolkit.database
{
    public class Database
    {
        private const string FilePath = "settings.properties";
        private readonly Dictionary<string, string> _data = new Dictionary<string, string>();

        public Database()
        {
            Load();
        }

        public void UpdateValue(string key, string value)
        {
            _data[key] = value;
            Save();
        }

        private void Save()
        {
            var temp = _data.Select(pair => pair.Key + "=" + pair.Value).ToList();
            File.WriteAllLines(FilePath, temp);
        }

        public string GetValue(string key, string defaultValue = "")
        {
            return _data.TryGetValue(key, out var value) ? value : defaultValue;
        }

        private void Load()
        {
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, "");
            }

            var lines = File.ReadAllLines(FilePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var split = line.Split('=');
                if (split.Length != 2) continue;
                _data.Add(split[0].Trim(), split[1].Trim());
            }
        }
    }
}