namespace AorusLcd.Core;

/// <summary>The role of a frame in an upload sequence, used to pace it correctly.</summary>
public enum UploadFrameKind
{
    Begin,
    Header,
    Chunk,
    End,
}

/// <summary>
/// A single 256-byte upload frame tagged with its <see cref="UploadFrameKind"/>.
/// Pacing is driven by <see cref="Kind"/> rather than by sniffing the frame
/// bytes, so payload chunks that happen to start with a control opcode can never
/// be misclassified.
/// </summary>
public readonly record struct UploadFrame(UploadFrameKind Kind, byte[] Data);
