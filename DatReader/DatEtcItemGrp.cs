namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed EtcItemgrp record from a Lineage 2 .dat file.
/// Structure: Helios version (Fafurion - Classic Secret of Empire).
/// </summary>
public sealed class DatEtcItemGrp
{
    public int Tag { get; init; }
    public uint ObjectId { get; init; }
    public int DropType { get; init; }
    public int DropAnimType { get; init; }
    public int DropRadius { get; init; }
    public int DropHeight { get; init; }
    public DropMeshData[] DropMeshes { get; init; } = [];
    public string Icon1 { get; init; } = string.Empty;
    public string Icon2 { get; init; } = string.Empty;
    public string Icon3 { get; init; } = string.Empty;
    public string Icon4 { get; init; } = string.Empty;
    public string Icon5 { get; init; } = string.Empty;
    public short Durability { get; init; }
    public short Weight { get; init; }
    public int MaterialType { get; init; }
    public int Crystallizable { get; init; }
    public short[] RelatedQuestIds { get; init; } = [];
    public int Color { get; init; }
    public int IsAttribution { get; init; }
    public short PropertyParams { get; init; }
    public string IconPanel { get; init; } = string.Empty;
    public string CompleteItemDropSoundType { get; init; } = string.Empty;
    public int InventoryType { get; init; }
    public string[] Meshes { get; init; } = [];
    public string[] Textures { get; init; } = [];
    public string DropSound { get; init; } = string.Empty;
    public string EquipSound { get; init; } = string.Empty;
    public int ConsumeType { get; init; }
    public uint EtcItemType { get; init; }
    public int CrystalType { get; init; }
}
