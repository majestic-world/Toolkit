namespace L2Toolkit.DatReader;

/// <summary>
/// Represents an MTX_NEW2 composite structure: mesh array + texture array.
/// </summary>
public sealed class MtxNew2
{
    public string[] Meshes { get; init; } = [];
    public string[] Textures { get; init; } = [];
}

/// <summary>
/// Represents an MTX3_NEW2 composite structure: mesh array with params + texture array + texext.
/// </summary>
public sealed class Mtx3New2
{
    public string[] Meshes { get; init; } = [];
    public (int Val1, sbyte Val2)[] MeshParams { get; init; } = [];
    public string[] Textures { get; init; } = [];
    public string TextExt { get; init; } = string.Empty;
}

/// <summary>
/// Drop mesh/texture data from a nested for-loop structure.
/// </summary>
public sealed class DropMeshData
{
    public string Mesh { get; init; } = string.Empty;
    public string[] Textures { get; init; } = [];
}
