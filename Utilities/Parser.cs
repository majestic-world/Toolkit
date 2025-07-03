using System;

namespace L2Toolkit.Utilities
{
    public class Parser
    {
        public static string GetValue(string text, string start, string end)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end)) return "";

            var startIndex = text.IndexOf(start, StringComparison.Ordinal);
            if (startIndex == -1) return "";

            startIndex += start.Length;

            var endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);
            return endIndex == -1 ? "" : text.Substring(startIndex, endIndex - startIndex);
        }
    }
}