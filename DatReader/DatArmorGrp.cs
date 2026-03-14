namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed Armorgrp record from a Lineage 2 .dat file.
/// Structure: Helios version (Fafurion - Classic Secret of Empire).
/// </summary>
public sealed class DatArmorGrp
{
    public static readonly string[] RaceSlotNames =
    [
        "m_HumnFigh", "f_HumnFigh",
        "m_DarkElf", "f_DarkElf",
        "m_Dorf", "f_Dorf",
        "m_Elf", "f_Elf",
        "m_HumnMyst", "f_HumnMyst",
        "m_OrcFigh", "f_OrcFigh",
        "m_OrcMage", "f_OrcMage",
        "m_Kamael", "f_Kamael",
        "mertheia", "fertheia",
        "NPC"
    ];

    public static readonly string[] RaceSlotAddNames =
    [
        "m_HumnFigh_add", "f_HumnFigh_add",
        "m_DarkElf_add", "f_DarkElf_add",
        "m_Dorf_add", "f_Dorf_add",
        "m_Elf_add", "f_Elf_add",
        "m_HumnMyst_add", "f_HumnMyst_add",
        "m_OrcFigh_add", "f_OrcFigh_add",
        "m_OrcMage_add", "f_OrcMage_add",
        "m_Kamael_add", "f_Kamael_add",
        "mertheia_mesh_add", "fertheia_mesh_add",
        "NPC_add"
    ];

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
    public int BodyPart { get; init; }

    /// <summary>19 race slots, each with MTX_NEW2 mesh data.</summary>
    public MtxNew2[] RaceMeshes { get; init; } = [];

    /// <summary>19 race slots, each with MTX3_NEW2 additional mesh data.</summary>
    public Mtx3New2[] RaceMeshesAdd { get; init; } = [];

    public string AttackEffect { get; init; } = string.Empty;
    public string[] ItemSounds { get; init; } = [];
    public string DropSound { get; init; } = string.Empty;
    public string EquipSound { get; init; } = string.Empty;
    public uint Unk7 { get; init; }
    public int Unk6 { get; init; }
    public int ArmorType { get; init; }
    public int CrystalType { get; init; }
    public short MpBonus { get; init; }
    public short HideMask { get; init; }
    public int UnderwearBodyPart1 { get; init; }
    public int UnderwearBodyPart2 { get; init; }
    public sbyte FullArmorEnchantEffectType { get; init; }
}
