using System;
using System.Buffers.Binary;
using System.Text;

namespace L2Toolkit.DatReader;

/// <summary>
/// Reads typed values from a Lineage 2 binary data buffer (little-endian).
/// </summary>
public sealed class L2BinaryReader
{
    private readonly byte[] _data;
    private int _position;

    public L2BinaryReader(byte[] data)
    {
        _data = data;
        _position = 0;
    }

    public int Position => _position;
    public int Remaining => _data.Length - _position;
    public bool HasData => _position < _data.Length;

    public byte ReadByte()
    {
        return _data[_position++];
    }

    public int ReadUByte()
    {
        return _data[_position++] & 0xFF;
    }

    public short ReadShort()
    {
        var value = BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(_position));
        _position += 2;
        return value;
    }

    public int ReadUShort()
    {
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_position));
        _position += 2;
        return value;
    }

    public int ReadInt()
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_position));
        _position += 4;
        return value;
    }

    public uint ReadUInt()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_position));
        _position += 4;
        return value;
    }

    public long ReadLong()
    {
        var value = BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(_position));
        _position += 8;
        return value;
    }

    public float ReadFloat()
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(_position));
        _position += 4;
        return value;
    }

    public double ReadDouble()
    {
        var value = BinaryPrimitives.ReadDoubleLittleEndian(_data.AsSpan(_position));
        _position += 8;
        return value;
    }

    /// <summary>
    /// Reads a compact variable-length integer (UE compact index format).
    /// </summary>
    public int ReadCompactInt()
    {
        int output = 0;
        bool signed = false;

        for (int i = 0; i < 5; i++)
        {
            int x = _data[_position++] & 0xFF;

            if (i == 0)
            {
                if ((x & 0x80) != 0)
                    signed = true;

                output |= x & 0x3F;

                if ((x & 0x40) == 0)
                    break;
            }
            else if (i == 4)
            {
                output |= (x & 0x1F) << 27;
            }
            else
            {
                output |= (x & 0x7F) << (6 + ((i - 1) * 7));

                if ((x & 0x80) == 0)
                    break;
            }
        }

        return signed ? -output : output;
    }

    /// <summary>
    /// Reads a UNICODE string: 4-byte LE size prefix, then UTF-16LE character data.
    /// </summary>
    public string ReadUnicodeString()
    {
        int size = ReadInt();
        if (size <= 0)
            return string.Empty;

        if (size > 1_000_000)
            throw new InvalidOperationException("Unicode string too large");

        var text = Encoding.Unicode.GetString(_data, _position, size);
        _position += size;
        return text;
    }

    /// <summary>
    /// Reads an ASCF string: compact-int length prefix, then either cp1252 or UTF-16LE data.
    /// Positive length = cp1252 with null terminator.
    /// Negative length = UTF-16LE with 2-byte null terminator.
    /// </summary>
    public string ReadAscfString()
    {
        int len = ReadCompactInt();
        if (len == 0)
            return string.Empty;

        if (len > 0)
        {
            // cp1252/Latin1 encoded, len bytes including null terminator
            var text = Encoding.Latin1.GetString(_data, _position, len - 1);
            _position += len;
            return text;
        }
        else
        {
            // UTF-16LE encoded, -len*2 bytes including 2-byte null terminator
            int byteCount = -len * 2;
            var text = Encoding.Unicode.GetString(_data, _position, byteCount - 2);
            _position += byteCount;
            return text;
        }
    }

    /// <summary>
    /// Reads a MAP_INT value (4-byte LE int used as index into L2GameDataName table).
    /// </summary>
    public int ReadMapInt()
    {
        return ReadInt();
    }

    public string ReadHex()
    {
        return (_data[_position++] & 0xFF).ToString("X2");
    }

    public string ReadRgb()
    {
        byte r = _data[_position++];
        byte g = _data[_position++];
        byte b = _data[_position++];
        return $"{r:X2}{g:X2}{b:X2}";
    }

    public string ReadRgba()
    {
        byte a = _data[_position++];
        byte r = _data[_position++];
        byte g = _data[_position++];
        byte b = _data[_position++];
        return $"{a:X2}{r:X2}{g:X2}{b:X2}";
    }

    public void Skip(int bytes)
    {
        _position += bytes;
    }
}
