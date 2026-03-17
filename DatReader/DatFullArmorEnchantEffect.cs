namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed FullArmorEnchantEffectData record from a Lineage 2 .dat file.
/// Structure: Valiance version.
/// isSafePackage="true" — SafePackage footer required on serialize.
/// Fields: effect_type UINT, unk UINT, min_enchant_num UINT,
///         noise_scale FLOAT, noise_pan_speed FLOAT, noise_rate FLOAT,
///         extrude_scale FLOAT, edge_peak FLOAT, edge_sharp FLOAT,
///         min_color RGBA, max_color RGBA, show_type UINT.
/// </summary>
public sealed class DatFullArmorEnchantEffect
{
    public uint   EffectType    { get; init; }
    public uint   Unk           { get; init; }
    public uint   MinEnchantNum { get; init; }
    public float  NoiseScale    { get; init; }
    public float  NoisePanSpeed { get; init; }
    public float  NoiseRate     { get; init; }
    public float  ExtrudeScale  { get; init; }
    public float  EdgePeak      { get; init; }
    public float  EdgeSharp     { get; init; }

    /// <summary>Min color — RGBA stored as AARRGGBB hex string (mutable for editing).</summary>
    public string MinColor { get; set; } = string.Empty;

    /// <summary>Max color — RGBA stored as AARRGGBB hex string (mutable for editing).</summary>
    public string MaxColor { get; set; } = string.Empty;

    public uint ShowType { get; init; }
}
