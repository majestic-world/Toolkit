using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace L2Toolkit.DatReader;

/// <summary>
/// Simple .l2dat file format for compressing text data files.
/// Format: [4B magic "L2DT"] [2B filename length] [filename UTF-8] [Brotli compressed data]
/// No encryption, no complex structure — just efficient compression with easy read/write.
/// </summary>
public static class L2Pack
{
    private static readonly byte[] Magic = "L2DT"u8.ToArray();

    /// <summary>
    /// Packs a text file into a compressed .l2dat file.
    /// </summary>
    public static void Pack(string inputPath, string outputPath)
    {
        var fileName = Path.GetFileName(inputPath);
        var fileNameBytes = Encoding.UTF8.GetBytes(fileName);
        var content = File.ReadAllBytes(inputPath);

        using var output = File.Create(outputPath);
        output.Write(Magic);
        output.Write(BitConverter.GetBytes((ushort)fileNameBytes.Length));
        output.Write(fileNameBytes);

        using var brotli = new BrotliStream(output, CompressionLevel.SmallestSize);
        brotli.Write(content);
    }

    /// <summary>
    /// Packs raw text content into a compressed .l2dat byte array.
    /// </summary>
    public static byte[] PackToBytes(string fileName, string textContent)
    {
        var fileNameBytes = Encoding.UTF8.GetBytes(fileName);
        var contentBytes = Encoding.UTF8.GetBytes(textContent);

        using var ms = new MemoryStream();
        ms.Write(Magic);
        ms.Write(BitConverter.GetBytes((ushort)fileNameBytes.Length));
        ms.Write(fileNameBytes);

        using (var brotli = new BrotliStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(contentBytes);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Unpacks a .l2dat file, returning the original file name and text content.
    /// </summary>
    public static (string FileName, string Content) Unpack(string l2datPath)
    {
        var data = File.ReadAllBytes(l2datPath);
        return Unpack(data);
    }

    /// <summary>
    /// Unpacks .l2dat data from a byte array.
    /// </summary>
    public static (string FileName, string Content) Unpack(byte[] data)
    {
        if (data.Length < 6 || data[0] != Magic[0] || data[1] != Magic[1] ||
            data[2] != Magic[2] || data[3] != Magic[3])
            throw new InvalidDataException("Not a valid .l2dat file.");

        var nameLen = BitConverter.ToUInt16(data, 4);
        var fileName = Encoding.UTF8.GetString(data, 6, nameLen);

        var compressedStart = 6 + nameLen;
        using var input = new MemoryStream(data, compressedStart, data.Length - compressedStart);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);

        return (fileName, Encoding.UTF8.GetString(output.ToArray()));
    }

    /// <summary>
    /// Extracts a .l2dat file back to its original text file in the specified directory.
    /// </summary>
    public static string ExtractToDirectory(string l2datPath, string outputDir)
    {
        var (fileName, content) = Unpack(l2datPath);
        var outputPath = Path.Combine(outputDir, fileName);
        File.WriteAllText(outputPath, content);
        return outputPath;
    }
}
