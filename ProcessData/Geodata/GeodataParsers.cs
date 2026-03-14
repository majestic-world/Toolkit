using System;
using System.IO;
using System.Text.RegularExpressions;

namespace L2Toolkit.ProcessData.Geodata;

public abstract class GeodataParser
{
    protected byte[] Data;
    protected int Pos;
    protected readonly string FilePath;
    protected readonly GeodataFormat Format;
    protected int[]? CachedXY;

    protected GeodataParser(GeodataFormat format, string filePath, byte[]? data = null)
    {
        Format = format;
        FilePath = filePath;
        Data = data ?? File.ReadAllBytes(filePath);
    }

    public static GeodataParser Create(GeodataFormat format, string filePath)
    {
        return format switch
        {
            GeodataFormat.L2J => new L2JParser(filePath),
            GeodataFormat.ConvDat => new ConvDatParser(filePath),
            GeodataFormat.L2D => new L2DParser(filePath),
            GeodataFormat.L2G => new L2GParser(filePath),
            GeodataFormat.L2S => new L2SParser(filePath),
            GeodataFormat.L2M => new L2MParser(filePath),
            GeodataFormat.RP => new RPParser(filePath),
            GeodataFormat.PathTxt => new PathTxtParser(filePath),
            _ => throw new ArgumentException($"Unsupported format: {format}")
        };
    }

    public virtual bool IsValid()
    {
        if (Data == null || Data.Length == 0) return false;
        var xy = GetXY();
        return xy[0] >= GeoConstants.MinX && xy[0] <= GeoConstants.MaxX &&
               xy[1] >= GeoConstants.MinY && xy[1] <= GeoConstants.MaxY;
    }

    public virtual void Decrypt() { }

    public virtual GeoRegion Parse()
    {
        var xy = GetXY();
        var region = new GeoRegion(xy[0], xy[1]);
        ReadBlocks(region);
        return region;
    }

    public abstract int[] GetXY();

    protected virtual void ReadBlocks(GeoRegion region)
    {
        for (int x = 0; x < GeoConstants.RegionSize; x++)
        {
            for (int y = 0; y < GeoConstants.RegionSize; y++)
            {
                int typeValue = ReadBlockTypeValue();
                var blockType = GeoConstants.GetBlockType(Format, typeValue);
                if (blockType == null) continue;

                var block = blockType.Value switch
                {
                    GeoBlockType.Flat => ReadFlatBlock(region),
                    GeoBlockType.Complex => ReadComplexBlock(region),
                    GeoBlockType.MultiLevel => ReadMultiLevelBlock(region),
                    _ => (GeoBlock?)null
                };

                if (block == null) continue;
                block.X = x;
                block.Y = y;
                region.AddBlock(x, y, block);
            }
        }
    }

    protected virtual int ReadBlockTypeValue() => Data[Pos++];

    protected virtual GeoBlock ReadFlatBlock(GeoRegion region)
    {
        short height = ReadInt16();
        var block = new GeoBlockFlat(region);
        var cell = new GeoMainCell(block, 0, 0, 1) { Height = height, Nswe = GeoConstants.NsweAll };
        block.AddCell(cell);
        return block;
    }

    protected virtual GeoBlock ReadComplexBlock(GeoRegion region)
    {
        var block = new GeoBlockComplex(region);
        block.InitCells(8, 8);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                block.SetLayers(x, y, 1);
                short raw = ReadInt16();
                var cell = new GeoMainCell(block, x, y, 1)
                {
                    Height = GeoMainCell.DecodeHeight(raw),
                    Nswe = GeoMainCell.DecodeNswe(raw)
                };
                block.AddCell(cell);
            }
        }
        return block;
    }

    protected virtual GeoBlock ReadMultiLevelBlock(GeoRegion region)
    {
        int startPos = Pos;
        var block = new GeoBlockMultiLevel(region);
        block.InitCells(8, 8);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                byte layerCount = Data[Pos++];
                block.SetLayers(x, y, layerCount);
                for (int l = 0; l < layerCount; l++)
                {
                    short raw = ReadInt16();
                    var cell = new GeoMainCell(block, x, y, l)
                    {
                        Height = GeoMainCell.DecodeHeight(raw),
                        Nswe = GeoMainCell.DecodeNswe(raw)
                    };
                    block.AddCell(cell);
                }
            }
        }
        block.SetConvDatTypeFromSize(Pos - startPos);
        return block;
    }

    protected short ReadInt16()
    {
        short v = BitConverter.ToInt16(Data, Pos);
        Pos += 2;
        return v;
    }

    protected int ReadInt32()
    {
        int v = BitConverter.ToInt32(Data, Pos);
        Pos += 4;
        return v;
    }

    protected int ReadInt32BE(int offset)
    {
        return (Data[offset] << 24) | (Data[offset + 1] << 16) |
               (Data[offset + 2] << 8) | Data[offset + 3];
    }

    protected static int[] ParseXYFromFileName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var parts = name.Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
            return [x, y];
        return [0, 0];
    }
}

public class L2JParser : GeodataParser
{
    public L2JParser(string filePath) : base(GeodataFormat.L2J, filePath) { }

    public override int[] GetXY() => CachedXY ??= ParseXYFromFileName(FilePath);
}

public class L2GParser : GeodataParser
{
    public L2GParser(string filePath) : base(GeodataFormat.L2G, filePath) { Pos = 4; }

    public override int[] GetXY() => CachedXY ??= ParseXYFromFileName(FilePath);

    public override void Decrypt()
    {
        int randomKey = ReadInt32BE(0);
        int checksum = GeoConstants.GeoChecksum ^ randomKey;
        if (checksum == 0) return;

        byte xorByte = (byte)((checksum >> 24 & 0xFF) ^ (checksum >> 16 & 0xFF) ^
                              (checksum >> 8 & 0xFF) ^ (checksum & 0xFF));

        for (int i = 4; i < Data.Length; i++)
        {
            byte encrypted = Data[i];
            byte decrypted = (byte)(encrypted ^ xorByte);
            Data[i] = decrypted;
            xorByte = decrypted;
        }
    }
}

public class L2SParser : GeodataParser
{
    public L2SParser(string filePath) : base(GeodataFormat.L2S, filePath) { Pos = 4; }

    public override int[] GetXY() => CachedXY ??= ParseXYFromFileName(FilePath);

    public override void Decrypt()
    {
        int checksum = GeoConstants.GeoChecksum;
        byte[] ipBytes = System.Text.Encoding.ASCII.GetBytes(GeoConstants.L2SBindIp);
        foreach (byte b in ipBytes)
        {
            checksum ^= b;
            checksum = (int)((uint)checksum >> 1 | (uint)checksum << 31);
        }

        checksum ^= ReadInt32BE(0);
        if (checksum == 0) return;

        byte xorByte = (byte)((checksum >> 24 & 0xFF) ^ (checksum >> 16 & 0xFF) ^
                              (checksum >> 8 & 0xFF) ^ (checksum & 0xFF));

        for (int i = 4; i < Data.Length; i++)
        {
            byte encrypted = Data[i];
            byte decrypted = (byte)(encrypted ^ xorByte);
            Data[i] = decrypted;
            xorByte = decrypted;
        }
    }
}

public class L2MParser : GeodataParser
{
    public L2MParser(string filePath) : base(GeodataFormat.L2M, filePath) { }

    public override int[] GetXY() => CachedXY ??= ParseXYFromFileName(FilePath);

    protected override GeoBlock ReadMultiLevelBlock(GeoRegion region)
    {
        int totalDataSize = 0;
        var block = new GeoBlockMultiLevel(region);
        block.InitCells(8, 8);

        int cellDataStart = Pos + 8 * 8 * 2; // after the cell header table
        int layerDataPos = cellDataStart;

        // First pass: read cell header table to determine layer counts and offsets
        int headerPos = Pos;
        var layerCounts = new int[8, 8];
        var offsets = new int[8, 8];

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                totalDataSize += 2;
                short headerValue = BitConverter.ToInt16(Data, headerPos);
                headerPos += 2;
                int count = headerValue & 0x1F;
                int offset = ((headerValue & 0xFFFF) >> 5) << 1;
                layerCounts[x, y] = count;
                offsets[x, y] = offset;
            }
        }

        // Calculate actual layer data position from first cell's offset
        int baseDataPos = Pos + 8 * 8 * 2; // position after all headers

        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                int count = layerCounts[x, y];
                block.SetLayers(x, y, count);
                int dataPos = baseDataPos + offsets[x, y];

                for (int l = 0; l < count; l++)
                {
                    totalDataSize += 2;
                    short raw = BitConverter.ToInt16(Data, dataPos);
                    dataPos += 2;
                    var cell = new GeoMainCell(block, x, y, l)
                    {
                        Height = GeoMainCell.DecodeHeight(raw),
                        Nswe = GeoMainCell.DecodeNswe(raw)
                    };
                    block.AddCell(cell);
                    if (dataPos > layerDataPos) layerDataPos = dataPos;
                }
            }
        }

        Pos = layerDataPos;
        block.SetConvDatTypeFromSize(totalDataSize);
        return block;
    }
}

public class ConvDatParser : GeodataParser
{
    private int[]? _header;

    public ConvDatParser(string filePath) : base(GeodataFormat.ConvDat, filePath) { }

    public override int[] GetXY()
    {
        if (CachedXY != null) return CachedXY;
        return CachedXY = Data != null && Data.Length >= 2
            ? [Data[0], Data[1]]
            : [0, 0];
    }

    public override bool IsValid()
    {
        ReadHeader();
        return _header![0] >= GeoConstants.MinX && _header[0] <= GeoConstants.MaxX &&
               _header[1] >= GeoConstants.MinY && _header[1] <= GeoConstants.MaxY &&
               base.IsValid();
    }

    public override GeoRegion Parse()
    {
        ReadHeader();
        return base.Parse();
    }

    private void ReadHeader()
    {
        if (_header != null) return;
        int savedPos = Pos;
        Pos = 0;
        _header = new int[7];
        _header[0] = Data[Pos++]; // X
        _header[1] = Data[Pos++]; // Y
        _header[2] = ReadInt16(); // 128
        _header[3] = ReadInt16(); // 16
        _header[4] = ReadInt32(); // cell count
        _header[5] = ReadInt32(); // flat+complex count
        _header[6] = ReadInt32(); // flat count
    }

    protected override int ReadBlockTypeValue()
    {
        short v = ReadInt16();
        return v;
    }

    protected override GeoBlock ReadFlatBlock(GeoRegion region)
    {
        short height = ReadInt16();
        short minHeight = ReadInt16();
        var block = new GeoBlockFlat(region);
        var cell = new GeoMainCell(block, 0, 0, 1)
        {
            Height = height,
            MinHeight = minHeight,
            Nswe = GeoConstants.NsweAll
        };
        block.AddCell(cell);
        return block;
    }

    protected override GeoBlock ReadMultiLevelBlock(GeoRegion region)
    {
        int startPos = Pos;
        var block = new GeoBlockMultiLevel(region);
        block.InitCells(8, 8);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                short layerCount = ReadInt16();
                block.SetLayers(x, y, layerCount);
                for (int l = 0; l < layerCount; l++)
                {
                    short raw = ReadInt16();
                    var cell = new GeoMainCell(block, x, y, l)
                    {
                        Height = GeoMainCell.DecodeHeight(raw),
                        Nswe = GeoMainCell.DecodeNswe(raw)
                    };
                    block.AddCell(cell);
                }
            }
        }
        block.SetConvDatTypeFromSize(Pos - startPos);
        return block;
    }
}

public class L2DParser : GeodataParser
{
    public L2DParser(string filePath) : this(GeodataFormat.L2D, filePath) { }
    protected L2DParser(GeodataFormat format, string filePath) : base(format, filePath) { }

    public override int[] GetXY() => CachedXY ??= ParseXYFromFileName(FilePath);

    protected override GeoBlock ReadComplexBlock(GeoRegion region)
    {
        var block = new GeoBlockComplex(region);
        block.InitCells(8, 8);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                block.SetLayers(x, y, 1);
                byte nswe = (byte)(Data[Pos++] & 0x0F);
                short height = ReadInt16();
                var cell = new GeoMainCell(block, x, y, 1)
                {
                    Height = height,
                    Nswe = nswe
                };
                block.AddCell(cell);
            }
        }
        return block;
    }

    protected override GeoBlock ReadMultiLevelBlock(GeoRegion region)
    {
        int startPos = Pos;
        var block = new GeoBlockMultiLevel(region);
        block.InitCells(8, 8);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                byte layerCount = Data[Pos++];
                block.SetLayers(x, y, layerCount);
                for (int l = 0; l < layerCount; l++)
                {
                    byte nswe = (byte)(Data[Pos++] & 0x0F);
                    short height = ReadInt16();
                    var cell = new GeoMainCell(block, x, y, l)
                    {
                        Height = height,
                        Nswe = nswe
                    };
                    block.AddCell(cell);
                }
            }
        }
        block.SetConvDatTypeFromSize(Pos - startPos);
        return block;
    }
}

public class RPParser : L2DParser
{
    public RPParser(string filePath) : base(GeodataFormat.RP, filePath) { }
}

public class PathTxtParser : GeodataParser
{
    private static readonly Regex Pattern = new(@"\[(\d*),(\d*)\](\d*)|\(([0-9\-:]*)\)", RegexOptions.Compiled);

    public PathTxtParser(string filePath) : base(GeodataFormat.PathTxt, filePath, []) { }

    public override int[] GetXY()
    {
        if (CachedXY != null) return CachedXY;
        var name = Path.GetFileName(FilePath);
        var parts = name.Split('_');
        if (parts.Length >= 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
            return CachedXY = [x, y];
        return CachedXY = [0, 0];
    }

    public override bool IsValid()
    {
        var xy = GetXY();
        return xy[0] >= GeoConstants.MinX && xy[0] <= GeoConstants.MaxX &&
               xy[1] >= GeoConstants.MinY && xy[1] <= GeoConstants.MaxY;
    }

    public override GeoRegion Parse()
    {
        var xy = GetXY();
        var region = new GeoRegion(xy[0], xy[1]);
        var grid = new string[2048][];
        for (int i = 0; i < 2048; i++)
            grid[i] = new string[2048];

        ReadTextFile(grid);
        var rawRegion = BuildRawBlocks(grid);
        NormalizeBlocks(region, rawRegion);
        return region;
    }

    private void ReadTextFile(string[][] grid)
    {
        foreach (var rawLine in File.ReadLines(FilePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line[0] != '[') continue;

            var match = Pattern.Match(line);
            if (match.Success && match.Groups[1].Success)
            {
                int x = int.Parse(match.Groups[1].Value);
                int y = int.Parse(match.Groups[2].Value);
                if (x < 2048 && y < 2048)
                    grid[x][y] = line;
            }
        }
    }

    private GeoRegion BuildRawBlocks(string[][] grid)
    {
        var xy = GetXY();
        var region = new GeoRegion(xy[0], xy[1]);

        for (int bx = 0; bx < GeoConstants.RegionSize; bx++)
        {
            for (int by = 0; by < GeoConstants.RegionSize; by++)
            {
                var block = new GeoBlockRaw(region);
                block.InitCells(8, 8);
                region.AddBlock(bx, by, block);

                for (int cx = 0; cx < 8; cx++)
                {
                    int gx = bx * 8 + cx;
                    for (int cy = 0; cy < 8; cy++)
                    {
                        int gy = by * 8 + cy;
                        var cellStr = grid[gx][gy];
                        if (string.IsNullOrEmpty(cellStr)) continue;

                        var match = Pattern.Match(cellStr);
                        if (!match.Success || !match.Groups[3].Success) continue;

                        int layers = int.Parse(match.Groups[3].Value);
                        if (layers == 0)
                        {
                            block.SetLayers(cx, cy, 1);
                            var cell = new GeoMainCell(block, cx, cy, 1) { Height = 16383, Nswe = 0 };
                            block.AddCell(cell);
                        }
                        else
                        {
                            block.SetLayers(cx, cy, layers);
                            for (int l = 0; l < layers; l++)
                            {
                                if (!match.NextMatch().Success) continue;
                                match = Pattern.Match(cellStr, match.Index + match.Length);
                                if (!match.Success) continue;

                                var values = ParseHeightAndNswe(match);
                                var cell = new GeoMainCell(block, cx, cy, l)
                                {
                                    Height = values.height,
                                    Nswe = values.nswe
                                };
                                block.AddCell(cell);
                            }
                        }
                    }
                }
            }
        }

        return region;
    }

    private void NormalizeBlocks(GeoRegion target, GeoRegion raw)
    {
        for (int bx = 0; bx < GeoConstants.RegionSize; bx++)
        {
            for (int by = 0; by < GeoConstants.RegionSize; by++)
            {
                var rawBlock = raw.Blocks[bx][by];
                if (rawBlock?.Cells == null) continue;

                var blockType = GuessBlockType(rawBlock);
                GeoBlock? block = blockType switch
                {
                    GeoBlockType.Flat => BuildFlatFromRaw(target, rawBlock),
                    GeoBlockType.Complex => BuildComplexFromRaw(target, rawBlock),
                    GeoBlockType.MultiLevel => BuildMultiLevelFromRaw(target, rawBlock),
                    _ => null
                };

                if (block == null) continue;
                block.X = bx;
                block.Y = by;
                target.AddBlock(bx, by, block);
            }
        }
        target.CellCount = raw.CellCount;
    }

    private static GeoBlockType GuessBlockType(GeoBlock rawBlock)
    {
        int maxLayers = 0;
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
                if (rawBlock.Cells[x][y] != null)
                    maxLayers = Math.Max(maxLayers, rawBlock.Cells[x][y].Length);

        if (maxLayers == 0) return GeoBlockType.Flat;
        if (maxLayers > 1) return GeoBlockType.MultiLevel;

        bool allNswe = true;
        short maxH = short.MinValue, minH = short.MaxValue;
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                if (rawBlock.Cells[x][y] == null || rawBlock.Cells[x][y].Length == 0) continue;
                var c = rawBlock.Cells[x][y][0];
                if (c == null) continue;
                allNswe = allNswe && c.Nswe == GeoConstants.NsweAll;
                if (c.Height > maxH) maxH = c.Height;
                if (c.Height < minH) minH = c.Height;
            }
        }

        if (allNswe && maxH == minH)
        {
            if (rawBlock.Cells[0][0]?.Length > 0 && rawBlock.Cells[0][0][0] != null)
            {
                rawBlock.Cells[0][0][0].Height = maxH;
                rawBlock.Cells[0][0][0].MinHeight = minH;
            }
            return GeoBlockType.Flat;
        }

        return GeoBlockType.Complex;
    }

    private static GeoBlock BuildFlatFromRaw(GeoRegion region, GeoBlock raw)
    {
        var block = new GeoBlockFlat(region);
        var src = raw.Cells[0][0][0];
        var cell = new GeoMainCell(block, 0, 0, 1)
        {
            Height = src.Height,
            MinHeight = src.GetMinHeight(),
            Nswe = GeoConstants.NsweAll
        };
        block.AddCell(cell);
        return block;
    }

    private static GeoBlock BuildComplexFromRaw(GeoRegion region, GeoBlock raw)
    {
        var block = new GeoBlockComplex(region);
        block.InitCells(8, 8);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                block.SetLayers(x, y, 1);
                var src = raw.Cells[x][y][0];
                short encoded = GeoMainCell.EncodeNsweAndHeight(src.Height, src.Nswe);
                var cell = new GeoMainCell(block, x, y, 1)
                {
                    Height = GeoMainCell.DecodeHeight(encoded),
                    Nswe = GeoMainCell.DecodeNswe(encoded)
                };
                block.AddCell(cell);
            }
        }
        return block;
    }

    private static GeoBlock BuildMultiLevelFromRaw(GeoRegion region, GeoBlock raw)
    {
        int dataSize = 0;
        var block = new GeoBlockMultiLevel(region);
        block.InitCells(8, 8);
        for (int x = 0; x < 8; x++)
        {
            for (int y = 0; y < 8; y++)
            {
                dataSize += 2;
                var layers = raw.Cells[x][y];
                block.SetLayers(x, y, layers.Length);
                for (int l = 0; l < layers.Length; l++)
                {
                    dataSize += 2;
                    var src = layers[l];
                    short encoded = GeoMainCell.EncodeNsweAndHeight(src.Height, src.Nswe);
                    var cell = new GeoMainCell(block, x, y, l)
                    {
                        Height = GeoMainCell.DecodeHeight(encoded),
                        Nswe = GeoMainCell.DecodeNswe(encoded)
                    };
                    block.AddCell(cell);
                }
            }
        }
        block.SetConvDatTypeFromSize(dataSize);
        return block;
    }

    private static (short height, byte nswe) ParseHeightAndNswe(Match match)
    {
        var content = match.Value;
        if (content.StartsWith('(')) content = content[1..];
        if (content.EndsWith(')')) content = content[..^1];

        var parts = content.Split(':');
        short height = short.Parse(parts[0]);
        byte nswe = 0;
        if (parts.Length > 1 && parts[1].Length >= 4)
        {
            var flags = parts[1];
            if (flags[0] == '1') nswe |= 1;  // E
            if (flags[1] == '1') nswe |= 2;  // W
            if (flags[2] == '1') nswe |= 4;  // S
            if (flags[3] == '1') nswe |= 8;  // N
        }
        return (height, nswe);
    }

    protected override void ReadBlocks(GeoRegion region) { }
}
