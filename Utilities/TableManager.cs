using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using L2Toolkit.DatReader;

namespace L2Toolkit.Utilities;


public static class TableManager
{
    public static readonly string TablesFolder =
        Path.Combine(AppContext.BaseDirectory, "tables");

    private static readonly ConcurrentDictionary<string, string> _cache = new();
    
    public static void EnsureTables()
    {
        Directory.CreateDirectory(TablesFolder);

        var assembly  = Assembly.GetExecutingAssembly();
        var prefix    = "L2Toolkit.Tables.";
        var suffix    = ".l2dat";

        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(prefix, StringComparison.Ordinal) ||
                !resourceName.EndsWith(suffix,   StringComparison.Ordinal))
                continue;

            var fileName = resourceName[prefix.Length..];
            var filePath = Path.Combine(TablesFolder, fileName);

            if (!File.Exists(filePath))
            {
                using var src  = assembly.GetManifestResourceStream(resourceName)!;
                using var dest = File.Create(filePath);
                src.CopyTo(dest);
            }
        }
    }
    
    public static string LoadTable(string tableName)
    {
        return _cache.GetOrAdd(tableName, name =>
        {
            var filePath = Path.Combine(TablesFolder, $"{name}.l2dat");
            byte[] bytes;

            if (File.Exists(filePath))
            {
                bytes = File.ReadAllBytes(filePath);
            }
            else
            {
                var assembly     = Assembly.GetExecutingAssembly();
                var resourceName = $"L2Toolkit.Tables.{name}.l2dat";
                using var stream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new FileNotFoundException($"Tabela não encontrada: {name}");
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                bytes = ms.ToArray();
            }

            var (_, content) = L2Pack.Unpack(bytes);
            return content;
        });
    }
    
    public static void InvalidateCache() => _cache.Clear();
}