using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace AorusLcd.Core.Nvapi;

/// <summary>Thin <c>nvapi64.dll</c> P/Invoke layer resolving needed QueryInterface ids; ids and <see cref="NvI2cInfoV3"/> match OpenRGB headers.</summary>
[SupportedOSPlatform("windows")]
internal static class NvApi
{
    private const uint IdInitialize = 0x0150E828;
    private const uint IdEnumPhysicalGpus = 0xE5AC921F;
    private const uint IdGetFullName = 0xCEEE8E9F;
    private const uint IdGetBusId = 0x1BE0B8E5;
    private const uint IdI2CWriteEx = 0x283AC65A;
    private const uint IdI2CReadEx = 0x4D7B0709;

    public const int MaxPhysicalGpus = 64;

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitializeDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int EnumPhysicalGpusDelegate([In, Out] IntPtr[] handles, out int count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetFullNameDelegate(IntPtr gpu, [Out] byte[] name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetBusIdDelegate(IntPtr gpu, out uint busId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int I2CTransferDelegate(IntPtr gpu, ref NvI2cInfoV3 info, ref uint unknown);

    private static readonly Lazy<Bindings> Api = new(Resolve);

    private sealed record Bindings(
        EnumPhysicalGpusDelegate EnumPhysicalGpus,
        GetFullNameDelegate GetFullName,
        GetBusIdDelegate GetBusId,
        I2CTransferDelegate I2CWriteEx,
        I2CTransferDelegate I2CReadEx);

    private static Bindings Resolve()
    {
        var init = GetDelegate<InitializeDelegate>(IdInitialize);
        int status = init();
        if (status != 0)
        {
            throw new NvApiException("NvAPI_Initialize", status);
        }
        return new Bindings(
            GetDelegate<EnumPhysicalGpusDelegate>(IdEnumPhysicalGpus),
            GetDelegate<GetFullNameDelegate>(IdGetFullName),
            GetDelegate<GetBusIdDelegate>(IdGetBusId),
            GetDelegate<I2CTransferDelegate>(IdI2CWriteEx),
            GetDelegate<I2CTransferDelegate>(IdI2CReadEx));
    }

    private static T GetDelegate<T>(uint id) where T : Delegate
    {
        var ptr = QueryInterface(id);
        if (ptr == IntPtr.Zero)
        {
            throw new NvApiException($"nvapi_QueryInterface(0x{id:X8})", -1);
        }
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    /// <summary>Enumerate physical GPU handles.</summary>
    public static IntPtr[] EnumPhysicalGpus()
    {
        var handles = new IntPtr[MaxPhysicalGpus];
        int status = Api.Value.EnumPhysicalGpus(handles, out int count);
        if (status != 0)
        {
            throw new NvApiException("NvAPI_EnumPhysicalGPUs", status);
        }
        return handles[..count];
    }

    /// <summary>Human-readable GPU name (e.g. "NVIDIA GeForce RTX 5090").</summary>
    public static string GetFullName(IntPtr gpu)
    {
        var name = new byte[64];
        int status = Api.Value.GetFullName(gpu, name);
        if (status != 0)
        {
            return "(unknown GPU)";
        }
        int end = Array.IndexOf(name, (byte)0);
        return System.Text.Encoding.ASCII.GetString(name, 0, end < 0 ? name.Length : end);
    }

    /// <summary>PCI bus number of the GPU, or null if NVAPI does not report it.</summary>
    public static uint? GetBusId(IntPtr gpu)
        => Api.Value.GetBusId(gpu, out uint busId) == 0 ? busId : null;

    public static int I2CWriteEx(IntPtr gpu, ref NvI2cInfoV3 info)
    {
        uint unknown = 0;
        return Api.Value.I2CWriteEx(gpu, ref info, ref unknown);
    }

    public static int I2CReadEx(IntPtr gpu, ref NvI2cInfoV3 info)
    {
        uint unknown = 0;
        return Api.Value.I2CReadEx(gpu, ref info, ref unknown);
    }
}
