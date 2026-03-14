namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed ItemStatData record from a Lineage 2 .dat file.
/// Structure: Helios version (Fafurion - Classic Secret of Empire).
/// </summary>
public sealed class DatItemStat
{
    public uint ObjectId { get; init; }
    public int PDefense { get; init; }
    public int MDefense { get; init; }
    public int PAttack { get; init; }
    public int MAttack { get; init; }
    public int PAttackSpeed { get; init; }
    public float PHit { get; init; }
    public float MHit { get; init; }
    public float PCritical { get; init; }
    public float MCritical { get; init; }
    public int Speed { get; init; }
    public int ShieldDefense { get; init; }
    public int ShieldDefenseRate { get; init; }
    public float PAvoid { get; init; }
    public float MAvoid { get; init; }
    public int PropertyParams { get; init; }
}
