namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed ItemName record from a Lineage 2 .dat file.
/// Structure: Fafurion - Classic Secret of Empire (GrandCrusade version).
/// </summary>
public sealed class DatItemName
{
    public uint Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string AdditionalName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public short Popup { get; init; }
    public string DefaultAction { get; init; } = string.Empty;
    public uint UseOrder { get; init; }
    public short NameClass { get; init; }
    public int Color { get; init; }
    public string TooltipTexture { get; init; } = string.Empty;
    public int IsTrade { get; init; }
    public int IsDrop { get; init; }
    public int IsDestruct { get; init; }
    public int IsPrivateStore { get; init; }
    public int KeepType { get; init; }
    public int IsNpcTrade { get; init; }
    public int IsCommissionStore { get; init; }
}
