namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed SkillName record from a Lineage 2 .dat file.
/// Structure: EtinasFate version (Fafurion - Classic Secret of Empire).
/// Uses indexed string table format.
/// </summary>
public sealed class DatSkillName
{
    public int SkillId { get; init; }
    public int SkillLevel { get; init; }
    public int SkillSublevel { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Desc { get; init; } = string.Empty;
    public string DescParam { get; init; } = string.Empty;
    public string EnchantName { get; init; } = string.Empty;
    public string EnchantNameParam { get; init; } = string.Empty;
    public string EnchantDesc { get; init; } = string.Empty;
    public string EnchantDescParam { get; init; } = string.Empty;
}
