namespace AorusLcd.Core;

/// <summary>The role of a frame in an upload sequence, used to pace it correctly.</summary>
public enum UploadFrameKind
{
    Begin,
    Header,
    Chunk,
    End,
}

/// <summary>256-byte upload frame tagged by <see cref="UploadFrameKind"/> so pacing never depends on payload bytes.</summary>
public readonly record struct UploadFrame(UploadFrameKind Kind, byte[] Data);
