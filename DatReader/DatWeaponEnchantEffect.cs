namespace L2Toolkit.DatReader;

/// <summary>
/// RGB_TEST compound type: 2× RGBA + 1× FLOAT = 12 bytes.
/// Used in WeaponEnchantEffectData (radiance and ring effect RGB fields).
/// Definition: R (RGBA) + R1 (RGBA) + B (FLOAT).
/// </summary>
public readonly struct RgbTest
{
    public string R  { get; init; }  // RGBA → AARRGGBB hex
    public string R1 { get; init; }  // RGBA → AARRGGBB hex
    public float  B  { get; init; }  // FLOAT
}

/// <summary>
/// Represents a parsed WeaponEnchantEffectData record from a Lineage 2 .dat file.
/// Structure: Helios version (Fafurion - Classic Secret of Empire).
/// isSafePackage="true" — SafePackage footer required on serialize.
/// </summary>
public sealed class DatWeaponEnchantEffect
{
    /// <summary>RGBA type identifier — stored as AARRGGBB hex string.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Grade string (ASCF), e.g. "S", "A".</summary>
    public string Grade { get; init; } = string.Empty;

    // ── Radiance effect arrays ────────────────────────────────────────────

    /// <summary>Raw MAP_INT indices for radiance effect names (for round-trip serialization).</summary>
    public int[]    RadianceNameIndices  { get; init; } = [];

    /// <summary>Resolved radiance effect names (for display).</summary>
    public string[] RadianceNames        { get; init; } = [];

    /// <summary>Radiance effect show-values (UINT per entry, count-prefixed).</summary>
    public uint[]   RadianceShowValues   { get; init; } = [];

    /// <summary>20 × RGB_TEST radiance opacity values.</summary>
    public RgbTest[] RadianceRgb         { get; init; } = new RgbTest[20];

    // ── Sword-flow effect ─────────────────────────────────────────────────

    /// <summary>Sword flow effect show value (single UINT).</summary>
    public uint SwordFlowShowValue { get; init; }

    /// <summary>20 × FLOAT sword flow max particle values.</summary>
    public float[] SwordFlowMaxParticle { get; init; } = new float[20];

    // ── Particle effect arrays ────────────────────────────────────────────

    /// <summary>Raw MAP_INT indices for particle effect names.</summary>
    public int[]    ParticleNameIndices { get; init; } = [];

    /// <summary>Resolved particle effect names (for display).</summary>
    public string[] ParticleNames       { get; init; } = [];

    /// <summary>Particle effect show-values (UINT per entry, count-prefixed).</summary>
    public uint[]   ParticleShowValues  { get; init; } = [];

    // ── Ring effect arrays ────────────────────────────────────────────────

    /// <summary>Raw MAP_INT indices for ring effect names.</summary>
    public int[]    RingNameIndices  { get; init; } = [];

    /// <summary>Resolved ring effect names (for display).</summary>
    public string[] RingNames        { get; init; } = [];

    /// <summary>Ring effect show-values (UINT per entry, count-prefixed).</summary>
    public uint[]   RingShowValues   { get; init; } = [];

    /// <summary>20 × RGB_TEST ring effect RGB values.</summary>
    public RgbTest[] RingRgb         { get; init; } = new RgbTest[20];
}
