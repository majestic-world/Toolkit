using System;
using System.Collections.Generic;

namespace L2Toolkit.Utilities
{
    public static class Parser
    {
        
        private static readonly string InvalidData = "Dados de parse inválidos!";
        
        public static string GetValue(string text, string start, string end)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end)) return "";

            var startIndex = text.IndexOf(start, StringComparison.Ordinal);
            if (startIndex == -1) return "";

            startIndex += start.Length;

            var endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);
            return endIndex == -1 ? "" : text.Substring(startIndex, endIndex - startIndex);
        }
        
        public static List<string> ParseId(string ids)
        {
            var list = new List<string>();
            if (ids.Contains("..."))
            {
                var parse = ids.Split("...");
                if (parse.Length != 2)
                {
                    throw new Exception(InvalidData);
                }

                int.TryParse(parse[0], out var initial);
                int.TryParse(parse[1], out var max);

                if (initial == 0 || max == 0)
                {
                    throw new Exception(InvalidData);
                }

                for (var i = initial; i <= max; i++)
                {
                    list.Add(i.ToString());
                }
            }
            else if (ids.Contains(';'))
            {
                var parse = ids.Split(";");
                foreach (var id in parse)
                {
                    list.Add(id);
                }
            }
            else
            {
                list.Add(ids);
            }

            return list;
        }
    }
}