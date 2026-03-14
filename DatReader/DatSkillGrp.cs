namespace L2Toolkit.DatReader;

/// <summary>
/// Represents a parsed Skillgrp record from a Lineage 2 .dat file.
/// Structure: Salvation version (Fafurion - Classic Secret of Empire).
/// </summary>
public sealed class DatSkillGrp
{
    public int SkillId { get; init; }
    public int SkillLevel { get; init; }
    public short SkillSublevel { get; init; }
    public int IconType { get; init; }
    public int MagicType { get; init; }
    public int OperateType { get; init; }
    public short MpConsume { get; init; }
    public int CastRange { get; init; }
    public int CastStyle { get; init; }
    public float HitTime { get; init; }
    public float CoolTime { get; init; }
    public float ReuseDelay { get; init; }
    public int EffectPoint { get; init; }
    public int IsMagic { get; init; }
    public short OriginSkill { get; init; }
    public int IsDouble { get; init; }
    public string[] Animations { get; init; } = [];
    public string SkillVisualEffect { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string IconPanel { get; init; } = string.Empty;
    public int Debuff { get; init; }
    public int ResistCast { get; init; }
    public int EnchantSkillLevel { get; init; }
    public string EnchantIcon { get; init; } = string.Empty;
    public short HpConsume { get; init; }
    public sbyte RumbleSelf { get; init; }
    public sbyte RumbleTarget { get; init; }
}
