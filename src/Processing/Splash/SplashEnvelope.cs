using System;
using System.Text;

namespace L2Toolkit.Processing.Splash;

/// <summary>Envelope XOR <c>Lineage2Ver###</c> que o cliente aplica sobre os BMP de splash.</summary>
public enum SplashEncryption
{
    None,
    Ver111,
    Ver121,
}

/// <summary>
/// Abre e fecha o envelope XOR das splash screens. A versão 111 usa uma chave fixa;
/// a 121 deriva a chave do nome do arquivo, então gravar com outro nome muda a chave.
/// </summary>
public static class SplashEnvelope
{
    private const int HeaderLength = 28;
    private const string Marker = "Lineage2Ver";
    private const byte Key111 = 0xAC;

    public static (byte[] Payload, SplashEncryption Encryption) Open(byte[] raw, string fileName)
    {
        if (raw.Length < HeaderLength)
            return (raw, SplashEncryption.None);

        var header = Encoding.Unicode.GetString(raw, 0, HeaderLength);
        if (!header.StartsWith(Marker, StringComparison.Ordinal))
            return (raw, SplashEncryption.None);

        var encryption = header[Marker.Length..] switch
        {
            "111" => SplashEncryption.Ver111,
            "121" => SplashEncryption.Ver121,
            var other => throw new NotSupportedException(
                $"A criptografia Lineage2Ver{other} não é suportada para splash screens (use 111 ou 121)."),
        };

        var key = KeyFor(encryption, fileName);
        var payload = new byte[raw.Length - HeaderLength];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)(raw[HeaderLength + i] ^ key);
        return (payload, encryption);
    }

    public static byte[] Seal(byte[] payload, SplashEncryption encryption, string fileName)
    {
        if (encryption == SplashEncryption.None)
            return payload;

        var header = Encoding.Unicode.GetBytes(encryption == SplashEncryption.Ver111 ? "Lineage2Ver111" : "Lineage2Ver121");
        var key = KeyFor(encryption, fileName);
        var output = new byte[header.Length + payload.Length];
        header.CopyTo(output, 0);
        for (var i = 0; i < payload.Length; i++)
            output[header.Length + i] = (byte)(payload[i] ^ key);
        return output;
    }

    /// <summary>Chave 121: byte baixo da soma das unidades UTF-16 do nome em minúsculas.</summary>
    private static byte KeyFor(SplashEncryption encryption, string fileName)
    {
        switch (encryption)
        {
            case SplashEncryption.Ver111:
                return Key111;
            case SplashEncryption.Ver121:
                var sum = 0;
                foreach (var unit in fileName.ToLowerInvariant())
                    sum += unit;
                return (byte)sum;
            case SplashEncryption.None:
                return 0;
            default:
                throw new ArgumentOutOfRangeException(nameof(encryption), encryption, null);
        }
    }
}
