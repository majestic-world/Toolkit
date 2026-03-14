using System;
using System.IO;

namespace L2Toolkit.ProcessData.Geodata;

public enum GeodataFormat
{
    L2J, ConvDat, L2D, L2S, L2G, RP, PathTxt, L2M
}

public enum GeoBlockType
{
    Flat = 0,
    Complex = 1,
    MultiLevel = 2
}

public static class GeoConstants
{
    public const int MinX = 10;
    public const int MaxX = 26;
    public const int MinY = 10;
    public const int MaxY = 26;
    public const short HeightMin = -16384;
    public const short HeightMax = 16376;
    public const int RegionSize = 256;
    public const string L2SBindIp = "127.0.0.1";
    public const byte NsweAll = 15;
    public const int GeoChecksum = -2126429781;

    public static string GetExtension(GeodataFormat format) => format switch
    {
        GeodataFormat.L2J => ".l2j",
        GeodataFormat.ConvDat => "_conv.dat",
        GeodataFormat.L2D => ".l2d",
        GeodataFormat.L2S => ".l2s",
        GeodataFormat.L2G => ".l2g",
        GeodataFormat.RP => ".rp",
        GeodataFormat.PathTxt => "_path.txt",
        GeodataFormat.L2M => ".l2m",
        _ => ""
    };

    public static GeodataFormat? DetectFormat(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (name.EndsWith("_conv.dat", StringComparison.OrdinalIgnoreCase)) return GeodataFormat.ConvDat;
        if (name.EndsWith("_path.txt", StringComparison.OrdinalIgnoreCase)) return GeodataFormat.PathTxt;
        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".l2j" => GeodataFormat.L2J,
            ".l2d" => GeodataFormat.L2D,
            ".l2s" => GeodataFormat.L2S,
            ".l2g" => GeodataFormat.L2G,
            ".l2m" => GeodataFormat.L2M,
            ".rp" => GeodataFormat.RP,
            _ => null
        };
    }

    public static GeoBlockType? GetBlockType(GeodataFormat format, int typeValue)
    {
        switch (format)
        {
            case GeodataFormat.ConvDat:
                if (typeValue == 0) return GeoBlockType.Flat;
                if (typeValue == 64) return GeoBlockType.Complex;
                return GeoBlockType.MultiLevel;
            case GeodataFormat.L2D:
            case GeodataFormat.RP:
                return (byte)typeValue switch
                {
                    0xD0 => GeoBlockType.Flat,
                    0xD1 => GeoBlockType.Complex,
                    0xD2 => GeoBlockType.MultiLevel,
                    _ => null
                };
            default:
                if (typeValue >= 0 && typeValue <= 2) return (GeoBlockType)typeValue;
                return null;
        }
    }

    public static string GetOutputFileName(int x, int y, GeodataFormat format)
    {
        return $"{x}_{y}{GetExtension(format)}";
    }
}

public class GeoMainCell
{
    public short Height { get; set; }
    public short? MinHeight { get; set; }
    public byte Nswe { get; set; }
    public int X { get; }
    public int Y { get; }
    public int Layer { get; }
    public GeoBlock? Block { get; }

    public GeoMainCell(GeoBlock? block, int x, int y, int layer)
    {
        Block = block;
        X = x;
        Y = y;
        Layer = layer;
    }

    public short GetMinHeight() => MinHeight ?? Height;

    public short GetHeightMask()
    {
        if (Block != null && Block.BlockType != GeoBlockType.Flat)
            return EncodeNsweAndHeight(Height, Nswe);
        return Height;
    }

    public static short DecodeHeight(short value)
    {
        short h = (short)(value & unchecked((short)0xFFF0));
        h = (short)(h >> 1);
        if (h <= GeoConstants.HeightMin) return GeoConstants.HeightMin;
        if (h >= GeoConstants.HeightMax) return GeoConstants.HeightMax;
        return h;
    }

    public static byte DecodeNswe(int value) => (byte)(value & 0x0F);

    public static short EncodeNsweAndHeight(short height, byte nswe)
    {
        short v = (short)(height << 1);
        v = (short)(v & unchecked((short)0xFFF0));
        v = (short)((ushort)v | (nswe & 0x0F));
        return v;
    }
}

public class GeoRegion
{
    public int X { get; }
    public int Y { get; }
    public GeoBlock[][] Blocks { get; }
    public int FlatBlocks { get; private set; }
    public int FlatAndComplexBlocks { get; private set; }
    public int CellCount { get; set; }

    public GeoRegion(int x, int y)
    {
        X = x;
        Y = y;
        Blocks = new GeoBlock[GeoConstants.RegionSize][];
        for (int i = 0; i < GeoConstants.RegionSize; i++)
            Blocks[i] = new GeoBlock[GeoConstants.RegionSize];
    }

    public void AddBlock(int x, int y, GeoBlock block)
    {
        Blocks[x][y] = block;
        if (block.BlockType == GeoBlockType.Flat) FlatBlocks++;
        if (block.BlockType != GeoBlockType.MultiLevel) FlatAndComplexBlocks++;
    }

    public void AddCellCount(int count) => CellCount += count;
}

public abstract class GeoBlock
{
    public GeoBlockType BlockType { get; }
    public GeoRegion Region { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public GeoMainCell[][][] Cells { get; set; } = null!;
    public short ConvDatType { get; set; }

    protected GeoBlock(GeoBlockType blockType, GeoRegion region)
    {
        BlockType = blockType;
        Region = region;
    }

    public void InitCells(int dimX, int dimY)
    {
        Cells = new GeoMainCell[dimX][][];
        for (int i = 0; i < dimX; i++)
        {
            Cells[i] = new GeoMainCell[dimY][];
            for (int j = 0; j < dimY; j++)
                Cells[i][j] = Array.Empty<GeoMainCell>();
        }
    }

    public void SetLayers(int x, int y, int count)
    {
        Cells[x][y] = new GeoMainCell[count];
        if (BlockType != GeoBlockType.Flat)
            Region.AddCellCount(count);
    }

    public void SetConvDatTypeFromSize(int dataSize)
    {
        ConvDatType = (short)(64 + (dataSize - 128));
    }

    public abstract void AddCell(GeoMainCell cell);
}

public class GeoBlockFlat : GeoBlock
{
    public GeoBlockFlat(GeoRegion region) : base(GeoBlockType.Flat, region)
    {
        InitCells(1, 1);
        SetLayers(0, 0, 1);
    }

    public override void AddCell(GeoMainCell cell) => Cells[0][0][0] = cell;
}

public class GeoBlockComplex : GeoBlock
{
    public GeoBlockComplex(GeoRegion region) : base(GeoBlockType.Complex, region) { }

    public override void AddCell(GeoMainCell cell) => Cells[cell.X][cell.Y][0] = cell;
}

public class GeoBlockMultiLevel : GeoBlock
{
    public GeoBlockMultiLevel(GeoRegion region) : base(GeoBlockType.MultiLevel, region) { }

    public override void AddCell(GeoMainCell cell) => Cells[cell.X][cell.Y][cell.Layer] = cell;
}

public class GeoBlockRaw : GeoBlock
{
    public GeoBlockRaw(GeoRegion region) : base(GeoBlockType.Flat, region) { }

    public override void AddCell(GeoMainCell cell) => Cells[cell.X][cell.Y][cell.Layer] = cell;
}
