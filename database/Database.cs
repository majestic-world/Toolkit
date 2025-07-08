using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace L2Toolkit.database
{
    public class Database
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "L2Toolkit",
            "settings.properties");

        private readonly Dictionary<string, string> _data = new();

        public Database()
        {
            
            var directory = Path.GetDirectoryName(FilePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            Load();
        }

        public void UpdateValue(string key, string value)
        {
            _data[key] = value;
            Save();
        }

        private void Save()
        {
            try
            {
                var temp = _data.Select(pair => pair.Key + "=" + pair.Value).ToList();
                File.WriteAllLines(FilePath, temp);
            }
            catch (Exception)
            {
                //ignore
            }
        }

        public int GetInt(string key, int defaultValue = 0)
        {
            var value = GetValue(key, defaultValue.ToString());
            return int.Parse(value);
        }

        public string GetValue(string key, string defaultValue = "")
        {
            return _data.GetValueOrDefault(key, defaultValue);
        }

        private void Load()
        {
            try
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
            catch (Exception)
            {
                //ignore
            }
        }
    }
}