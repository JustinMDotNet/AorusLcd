namespace AorusLcd.Core.Nvapi;

/// <summary>Thrown when an NVAPI call returns a non-zero status code.</summary>
public sealed class NvApiException(string operation, int status)
    : Exception($"{operation} failed (NVAPI status {status})")
{
    public int Status { get; } = status;
}
