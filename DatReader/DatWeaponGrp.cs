namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed Weapongrp record from a Lineage 2 .dat file.
/// Structure: Helios version (Fafurion - Classic Secret of Empire).
/// </summary>
public sealed class DatWeaponGrp
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
    public int BodyPart { get; init; }
    public int Handness { get; init; }

    // wp_mesh
    public string[] WpMeshes { get; init; } = [];
    public int[] WpMeshFlags { get; init; } = [];

    public string[] Textures { get; init; } = [];
    public string[] ItemSounds { get; init; } = [];
    public string DropSound { get; init; } = string.Empty;
    public string EquipSound { get; init; } = string.Empty;
    public string Effect { get; init; } = string.Empty;
    public int RandomDamage { get; init; }
    public int WeaponType { get; init; }
    public int CrystalType { get; init; }
    public int MpConsume { get; init; }
    public int SoulshotCount { get; init; }
    public int SpiritshotCount { get; init; }
    public short Curvature { get; init; }
    public int Unk10 { get; init; }
    public sbyte CanEquipHero { get; init; }
    public int IsMagicWeapon { get; init; }
    public float ErtheiaFistScale { get; init; }
    public short Junk { get; init; }

    // Enchanted effects
    public EnchantedEffectData[] EnchantedEffects { get; init; } = [];

    // Variation
    public sbyte[] VariationEffectTypes { get; init; } = [];
    public string[] VariationIcons { get; init; } = [];
    public int NormalEnsoulCount { get; init; }
    public int SpecialEnsoulCount { get; init; }
}

public sealed class EnchantedEffectData
{
    public string Effect { get; init; } = string.Empty;
    public float OffsetX { get; init; }
    public float OffsetY { get; init; }
    public float OffsetZ { get; init; }
    public float MeshOffsetX { get; init; }
    public float MeshOffsetY { get; init; }
    public float MeshOffsetZ { get; init; }
    public float MeshScaleX { get; init; }
    public float MeshScaleY { get; init; }
    public float MeshScaleZ { get; init; }
    public float Velocity { get; init; }
    public float ParticleScale { get; init; }
    public float EffectScale { get; init; }
    public float ParticleOffsetX { get; init; }
    public float ParticleOffsetY { get; init; }
    public float ParticleOffsetZ { get; init; }
    public float RingOffsetX { get; init; }
    public float RingOffsetY { get; init; }
    public float RingOffsetZ { get; init; }
    public float RingScaleX { get; init; }
    public float RingScaleY { get; init; }
    public float RingScaleZ { get; init; }
}
