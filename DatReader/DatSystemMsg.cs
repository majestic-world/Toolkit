namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed SystemMsg record from a Lineage 2 .dat file.
/// Structure: Helios version (Fafurion - Classic Secret of Empire).
/// </summary>
public sealed class DatSystemMsg
{
    public uint Id { get; init; }
    public uint Unk0 { get; init; }
    public string Message { get; init; } = string.Empty;
    public uint Group { get; init; }
    public string Color { get; init; } = string.Empty;   // RGBA → AARRGGBB hex
    public string Sound { get; init; } = string.Empty;   // MAP_INT resolved
    public string Voice { get; init; } = string.Empty;   // MAP_INT resolved
    public uint Win { get; init; }
    public uint Font { get; init; }
    public uint LfTime { get; init; }
    public uint Bkg { get; init; }
    public uint Anim { get; init; }
    public string ScrnMsg { get; init; } = string.Empty;
    public string GfxScrnMsg { get; init; } = string.Empty;
    public string GfxScrnParam { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
}
