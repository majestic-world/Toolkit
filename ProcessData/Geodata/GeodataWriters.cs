using System;
using System.IO;

namespace L2Toolkit.ProcessData.Geodata;

public abstract class GeodataWriter
{
    protected readonly GeoRegion Region;

    protected GeodataWriter(GeoRegion region)
    {
        Region = region;
    }

    public static GeodataWriter Create(GeoRegion region, GeodataFormat format)
    {
        return format switch
        {
            GeodataFormat.L2J => new L2JWriter(region),
            GeodataFormat.ConvDat => new ConvDatWriter(region),
            GeodataFormat.L2G => new L2GWriter(region),
            _ => throw new ArgumentException($"Unsupported output format: {format}")
        };
    }

    public byte[] Write()
    {
        using var ms = new MemoryStream();
        WriteHeader(ms);
        WriteBlocks(ms);
        var data = ms.ToArray();
        return Encrypt(data);
    }

    public void WriteTo(string filePath)
    {
        File.WriteAllBytes(filePath, Write());
    }

    protected virtual void WriteHeader(MemoryStream ms) { }
    protected virtual byte[] Encrypt(byte[] data) => data;

    private void WriteBlocks(MemoryStream ms)
    {
        for (int x = 0; x < GeoConstants.RegionSize; x++)
        {
            for (int y = 0; y < GeoConstants.RegionSize; y++)
            {
                var block = Region.Blocks[x][y];
                byte[] blockData = block.BlockType switch
                {
                    GeoBlockType.Flat => WriteFlatBlock(block),
                    GeoBlockType.Complex => WriteComplexBlock(block),
                    GeoBlockType.MultiLevel => WriteMultiLevelBlock(block),
                    _ => []
                };
                ms.Write(blockData);
            }
        }
    }

    protected abstract byte[] WriteFlatBlock(GeoBlock block);
    protected abstract byte[] WriteComplexBlock(GeoBlock block);
    protected abstract byte[] WriteMultiLevelBlock(GeoBlock block);

    protected static void WriteInt16LE(MemoryStream ms, short value)
    {
        ms.WriteByte((byte)(value & 0xFF));
        ms.WriteByte((byte)((value >> 8) & 0xFF));
    }

    protected static void WriteInt32LE(MemoryStream ms, int value)
    {
        ms.WriteByte((byte)(value & 0xFF));
        ms.WriteByte((byte)((value >> 8) & 0xFF));
        ms.WriteByte((byte)((value >> 16) & 0xFF));
        ms.WriteByte((byte)((value >> 24) & 0xFF));
    }
}

public class L2JWriter : GeodataWriter
{
    public L2JWriter(GeoRegion region) : base(region) { }

    protected override byte[] WriteFlatBlock(GeoBlock block)
    {
        using var ms = new MemoryStream(3);
        ms.WriteByte((byte)GeoBlockType.Flat);
        var cell = block.Cells[0][0][0];
        WriteInt16LE(ms, cell.GetHeightMask());
        return ms.ToArray();
    }

    protected override byte[] WriteComplexBlock(GeoBlock block)
    {
        using var ms = new MemoryStream(1 + 8 * 8 * 2);
        ms.WriteByte((byte)GeoBlockType.Complex);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var cell = block.Cells[x][y][0];
                WriteInt16LE(ms, cell.GetHeightMask());
            }
        return ms.ToArray();
    }

    protected override byte[] WriteMultiLevelBlock(GeoBlock block)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)GeoBlockType.MultiLevel);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                int layers = block.Cells[x][y].Length;
                ms.WriteByte((byte)layers);
                for (int l = 0; l < layers; l++)
                {
                    var cell = block.Cells[x][y][l];
                    WriteInt16LE(ms, cell.GetHeightMask());
                }
            }
        return ms.ToArray();
    }
}

public class ConvDatWriter : GeodataWriter
{
    public ConvDatWriter(GeoRegion region) : base(region) { }

    protected override void WriteHeader(MemoryStream ms)
    {
        ms.WriteByte((byte)Region.X);
        ms.WriteByte((byte)Region.Y);
        WriteInt16LE(ms, 128);
        WriteInt16LE(ms, 16);
        WriteInt32LE(ms, Region.CellCount);
        WriteInt32LE(ms, Region.FlatAndComplexBlocks);
        WriteInt32LE(ms, Region.FlatBlocks);
    }

    protected override byte[] WriteFlatBlock(GeoBlock block)
    {
        using var ms = new MemoryStream(6);
        WriteInt16LE(ms, 0); // FLAT type
        var cell = block.Cells[0][0][0];
        WriteInt16LE(ms, cell.GetHeightMask());
        WriteInt16LE(ms, GeoMainCell.EncodeNsweAndHeight(cell.GetMinHeight(), cell.Nswe));
        return ms.ToArray();
    }

    protected override byte[] WriteComplexBlock(GeoBlock block)
    {
        using var ms = new MemoryStream(2 + 8 * 8 * 2);
        WriteInt16LE(ms, 64); // COMPLEX type
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var cell = block.Cells[x][y][0];
                WriteInt16LE(ms, cell.GetHeightMask());
            }
        return ms.ToArray();
    }

    protected override byte[] WriteMultiLevelBlock(GeoBlock block)
    {
        using var ms = new MemoryStream();
        WriteInt16LE(ms, block.ConvDatType);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                int layers = block.Cells[x][y].Length;
                WriteInt16LE(ms, (short)layers);
                for (int l = 0; l < layers; l++)
                {
                    var cell = block.Cells[x][y][l];
                    WriteInt16LE(ms, cell.GetHeightMask());
                }
            }
        return ms.ToArray();
    }
}

public class L2GWriter : GeodataWriter
{
    private readonly int _randomKey;
    private readonly int _checksum;

    public L2GWriter(GeoRegion region) : base(region)
    {
        var rng = new Random();
        _randomKey = rng.Next(int.MinValue, int.MaxValue);
        _checksum = GeoConstants.GeoChecksum ^ _randomKey;
    }

    protected override byte[] WriteFlatBlock(GeoBlock block)
    {
        using var ms = new MemoryStream(3);
        ms.WriteByte((byte)GeoBlockType.Flat);
        var cell = block.Cells[0][0][0];
        WriteInt16LE(ms, cell.GetHeightMask());
        return ms.ToArray();
    }

    protected override byte[] WriteComplexBlock(GeoBlock block)
    {
        using var ms = new MemoryStream(1 + 8 * 8 * 2);
        ms.WriteByte((byte)GeoBlockType.Complex);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                var cell = block.Cells[x][y][0];
                WriteInt16LE(ms, cell.GetHeightMask());
            }
        return ms.ToArray();
    }

    protected override byte[] WriteMultiLevelBlock(GeoBlock block)
    {
        using var ms = new MemoryStream();
        ms.WriteByte((byte)GeoBlockType.MultiLevel);
        for (int x = 0; x < 8; x++)
            for (int y = 0; y < 8; y++)
            {
                int layers = block.Cells[x][y].Length;
                ms.WriteByte((byte)layers);
                for (int l = 0; l < layers; l++)
                {
                    var cell = block.Cells[x][y][l];
                    WriteInt16LE(ms, cell.GetHeightMask());
                }
            }
        return ms.ToArray();
    }

    protected override byte[] Encrypt(byte[] data)
    {
        byte xorByte = (byte)((_checksum >> 24 & 0xFF) ^ (_checksum >> 16 & 0xFF) ^
                              (_checksum >> 8 & 0xFF) ^ (_checksum & 0xFF));

        var result = new byte[data.Length + 4];

        // Write random key as big-endian 4 bytes
        result[0] = (byte)((_randomKey >> 24) & 0xFF);
        result[1] = (byte)((_randomKey >> 16) & 0xFF);
        result[2] = (byte)((_randomKey >> 8) & 0xFF);
        result[3] = (byte)(_randomKey & 0xFF);

        for (int i = 0; i < data.Length; i++)
        {
            byte plain = data[i];
            result[i + 4] = (byte)(plain ^ xorByte);
            xorByte = plain;
        }

        return result;
    }
}
