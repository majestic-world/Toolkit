using System;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text;

namespace L2Toolkit.DatReader;

/// <summary>
/// Decrypts Lineage 2 .dat files using RSA + zlib decompression.
/// Supports header versions 411-414 (Fafurion era).
/// </summary>
public static class DatCrypto
{
    private const int HeaderSize = 28;
    private const int FooterSize = 20;
    private const int RsaBlockSize = 128;

    // v413_encdec private key (RSA private exponent for re-encryption)
    private static readonly BigInteger V413EncModulus = ParseHex(
        "75B4D6DE5C016544068A1ACF125869F43D2E09FC55B8B1E289556DAF9B8757635593446288B3653DA1CE91C87BB1A5C18F16323495C55D7D72C0890A83F69BFD1FD9434EB1C02F3E4679EDFA43309319070129C267C85604D87BB65BAE205DE3707AF1D2108881ABB567C3B3D069AE67C3A4C6A3AA93D26413D4C66094AE2039");
    private static readonly BigInteger V413EncPrivateExp = ParseHex(
        "30b4c2d798d47086145c75063c8e841e719776e400291d7838d3e6c4405b504c6a07f8fca27f32b86643d2649d1d5f124cdd0bf272f0909dd7352fe10a77b34d831043d9ae541f8263c6fe3d1c14c2f04e43a7253a6dda9a8c1562cbd493c1b631a1957618ad5dfe5ca28553f746e2fc6f2db816c7db223ec91e955081c1de65");

    /// <summary>
    /// Encrypts raw binary data using the v413_encdec RSA private key + zlib.
    /// Produces a valid Lineage2Ver413 .dat file readable by a client with the matching public key.
    /// </summary>
    public static byte[] EncryptFile(byte[] decryptedData)
    {
        // Step 1: zlib compress with 4-byte LE size prefix
        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            var sizePrefix = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(sizePrefix, decryptedData.Length);
            ms.Write(sizePrefix);
            using (var zlib = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(decryptedData);
            compressed = ms.ToArray();
        }

        // Step 2: RSA encrypt in 128-byte blocks (mirrors Java RSADatCrypter.encryptResult)
        using var rsaOut = new MemoryStream();
        var buffer = new byte[124];
        var block  = new byte[128];
        int offset = 0;

        while (offset < compressed.Length)
        {
            int len = Math.Min(124, compressed.Length - offset);
            Buffer.BlockCopy(compressed, offset, buffer, 0, len);
            offset += len;

            Array.Clear(block, 0, 128);
            block[0] = (byte)((len >> 24) & 0xFF);
            block[1] = (byte)((len >> 16) & 0xFF);
            block[2] = (byte)((len >> 8)  & 0xFF);
            block[3] = (byte)(len         & 0xFF);

            // Right-align data with 4-byte alignment padding at the end
            int pad = (-len) & 3;
            int pos = 128 - len - pad;
            Buffer.BlockCopy(buffer, 0, block, pos, len);

            rsaOut.Write(RsaEncryptBlock(block, V413EncPrivateExp, V413EncModulus));
        }

        // Step 3: assemble: header (28 bytes) + RSA payload + footer (20 bytes)
        var header  = Encoding.Unicode.GetBytes("Lineage2Ver413");
        var footer  = new byte[] { 0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,100 };
        var payload = rsaOut.ToArray();

        var result = new byte[header.Length + payload.Length + footer.Length];
        Buffer.BlockCopy(header,  0, result, 0,                                   header.Length);
        Buffer.BlockCopy(payload, 0, result, header.Length,                       payload.Length);
        Buffer.BlockCopy(footer,  0, result, header.Length + payload.Length,      footer.Length);
        return result;
    }

    private static byte[] RsaEncryptBlock(byte[] block, BigInteger privateExp, BigInteger modulus)
    {
        var plain     = new BigInteger(block, isUnsigned: true, isBigEndian: true);
        var encrypted = BigInteger.ModPow(plain, privateExp, modulus);
        var enc       = encrypted.ToByteArray(isUnsigned: true, isBigEndian: true);

        var result = new byte[RsaBlockSize];
        int off = RsaBlockSize - enc.Length;
        if (off >= 0)
            Buffer.BlockCopy(enc, 0,    result, off,  enc.Length);
        else
            Buffer.BlockCopy(enc, -off, result, 0,    RsaBlockSize);
        return result;
    }

    public static byte[] DecryptFile(string filePath)
    {
        var data = File.ReadAllBytes(filePath);
        var version = ReadHeader(data);
        var keys = GetRsaKeys(version);

        foreach (var (modulus, exponent) in keys)
        {
            var result = TryDecryptWithKey(data, modulus, exponent);
            if (result != null)
                return result;
        }

        throw new InvalidDataException($"Failed to decrypt .dat file with any known key for version {version}");
    }

    private static byte[]? TryDecryptWithKey(byte[] data, BigInteger modulus, BigInteger exponent)
    {
        var encryptedStart = HeaderSize;
        var encryptedEnd = data.Length - FooterSize;

        using var rsaResult = new MemoryStream();

        for (var offset = encryptedStart; offset < encryptedEnd; offset += RsaBlockSize)
        {
            if (offset + RsaBlockSize > encryptedEnd)
                break;

            var block = new byte[RsaBlockSize];
            Buffer.BlockCopy(data, offset, block, 0, RsaBlockSize);

            var decrypted = RsaDecryptBlock(block, exponent, modulus);

            int size = (decrypted[0] << 24) | (decrypted[1] << 16) | (decrypted[2] << 8) | decrypted[3];

            if (size <= 0 || size > RsaBlockSize)
                return null; // Wrong key

            int pad = (-size & 0x1) + (-size & 0x2);
            int start = RsaBlockSize - size - pad;
            rsaResult.Write(decrypted, start, size);
        }

        if (rsaResult.Length < 4)
            return null;

        var compressed = rsaResult.ToArray();

        int inflatedSize = compressed[0]
                         | (compressed[1] << 8)
                         | (compressed[2] << 16)
                         | (compressed[3] << 24);

        try
        {
            using var compressedStream = new MemoryStream(compressed, 4, compressed.Length - 4);
            using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
            using var output = new MemoryStream(inflatedSize);
            zlibStream.CopyTo(output);
            return output.ToArray();
        }
        catch
        {
            return null; // Decompression failed — wrong key
        }
    }

    public static string ReadHeader(byte[] data)
    {
        if (data.Length < HeaderSize)
            throw new InvalidDataException("File too small for Lineage2 .dat header");

        var header = Encoding.Unicode.GetString(data, 0, HeaderSize);
        if (!header.StartsWith("Lineage2Ver"))
            throw new InvalidDataException($"Invalid .dat header: '{header}'");

        return header[11..];
    }

    private static byte[] RsaDecryptBlock(byte[] block, BigInteger exponent, BigInteger modulus)
    {
        var cipher = new BigInteger(block.AsSpan(), isUnsigned: true, isBigEndian: true);
        var plain = BigInteger.ModPow(cipher, exponent, modulus);
        var plainBytes = plain.ToByteArray(isUnsigned: true, isBigEndian: true);

        // Pad result to 128 bytes (big-endian, left-pad with zeros)
        var result = new byte[RsaBlockSize];
        var offset = RsaBlockSize - plainBytes.Length;
        if (offset >= 0)
            Buffer.BlockCopy(plainBytes, 0, result, offset, plainBytes.Length);
        else
            Buffer.BlockCopy(plainBytes, -offset, result, 0, RsaBlockSize);

        return result;
    }

    private static (BigInteger modulus, BigInteger exponent)[] GetRsaKeys(string version)
    {
        return version switch
        {
            "411" => [(ParseHex("8c9d5da87b30f5d7cd9dc88c746eaac5bb180267fa11737358c4c95d9adf59dd37689f9befb251508759555d6fe0eca87bebe0a10712cf0ec245af84cd22eb4cb675e98eaf5799fca62a20a2baa4801d5d70718dcd43283b8428f1387aec6600f937bfc7bb72404d187d3a9c438f1ffce9ce365dccf754232ff6def038a41385"), new BigInteger(0x1d))],
            "412" => [(ParseHex("a465134799cf2c45087093e7d0f0f144e6d528110c08f674730d436e40827330eccea46e70acf10cdda7d8f710e3b44dcca931812d76cd7494289bca8b73823f57efc0515b97e4a2a02612ccfa719cf7885104b06f2e7e2cc967b62e3d3b1aadb925db94cbc8cd3070a4bb13f7e202c7733a67b1b94c1ebc0afcbe1a63b448cf"), new BigInteger(0x25))],
            "413" => [
                (ParseHex("97df398472ddf737ef0a0cd17e8d172f0fef1661a38a8ae1d6e829bc1c6e4c3cfc19292dda9ef90175e46e7394a18850b6417d03be6eea274d3ed1dde5b5d7bde72cc0a0b71d03608655633881793a02c9a67d9ef2b45eb7c08d4be329083ce450e68f7867b6749314d40511d09bc5744551baa86a89dc38123dc1668fd72d83"), new BigInteger(0x35)),
                (ParseHex("75B4D6DE5C016544068A1ACF125869F43D2E09FC55B8B1E289556DAF9B8757635593446288B3653DA1CE91C87BB1A5C18F16323495C55D7D72C0890A83F69BFD1FD9434EB1C02F3E4679EDFA43309319070129C267C85604D87BB65BAE205DE3707AF1D2108881ABB567C3B3D069AE67C3A4C6A3AA93D26413D4C66094AE2039"), new BigInteger(0x1d))
            ],
            "414" => [(ParseHex("ad70257b2316ce09dfaf2ebc3f63b3d673b0c98a403950e26bb87379b11e17aed0e45af23e7171e5ec1fbc8d1ae32ffb7801b31266eef9c334b53469d4b7cbe83284273d35a9aab49b453e7012f374496c65f8089f5d134b0eb3d1e3b22051ed5977a6dd68c4f85785dfcc9f4412c81681944fc4b8ce27caf0242deaa5762e8d"), new BigInteger(0x25))],
            _ => throw new NotSupportedException($"Unsupported .dat version: Lineage2Ver{version}")
        };
    }

    private static BigInteger ParseHex(string hex)
    {
        return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
    }
}
