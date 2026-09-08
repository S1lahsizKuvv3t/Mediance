using System.Runtime.InteropServices;

namespace Mediance.Windows.Audio;

// Windows exposes this factory to its own volume mixer, but does not publish it
// as a supported high-level API. Keep all ABI-sensitive details in this file.
internal sealed unsafe class AudioPolicyConfigAdapter : IDisposable
{
    private const string ActivatableClass = "Windows.Media.Internal.AudioPolicyConfig";
    private const string MmDevicePrefix = @"\\?\SWD#MMDEVAPI#";
    private const string RenderSuffix = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
    private static readonly Guid FactoryId = new("ab3d4648-e242-459f-b02f-541c70306324");
    private nint _factory;

    internal AudioPolicyConfigAdapter()
    {
        var iid = FactoryId;
        nint className = 0;
        nint factoryPointer = 0;
        try
        {
            ThrowIfFailed(NativeMethods.WindowsCreateString(ActivatableClass, (uint)ActivatableClass.Length,
                out className), "create activation identifier");
            ThrowIfFailed(NativeMethods.RoGetActivationFactory(className, ref iid, out factoryPointer),
                "activate audio policy");
            _factory = factoryPointer;
            factoryPointer = 0;
        }
        finally
        {
            if (factoryPointer != 0) Marshal.Release(factoryPointer);
            if (className != 0) _ = NativeMethods.WindowsDeleteString(className);
        }
    }

    internal string? GetPersistedRenderEndpoint(uint processId)
    {
        nint packedId = 0;
        try
        {
            var vtable = *(nint**)_factory;
            var get = (delegate* unmanaged[Stdcall]<nint, uint, int, int, nint*, int>)vtable[26];
            var hr = get(_factory, processId, (int)DataFlow.Render, (int)Role.Multimedia, &packedId);
            ThrowIfFailed(hr, "read");
            if (packedId == 0) return null;
            var buffer = NativeMethods.WindowsGetStringRawBuffer(packedId, out var length);
            var value = Marshal.PtrToStringUni(buffer, checked((int)length));
            return string.IsNullOrWhiteSpace(value) ? null : Unpack(value);
        }
        finally
        {
            if (packedId != 0) _ = NativeMethods.WindowsDeleteString(packedId);
        }
    }

    internal void SetPersistedRenderEndpoint(uint processId, string? endpointId)
    {
        nint hstring = 0;
        try
        {
            if (!string.IsNullOrWhiteSpace(endpointId))
            {
                var packed = Pack(endpointId);
                ThrowIfFailed(NativeMethods.WindowsCreateString(packed, (uint)packed.Length, out hstring),
                    "create endpoint identifier");
            }

            var vtable = *(nint**)_factory;
            var set = (delegate* unmanaged[Stdcall]<nint, uint, int, int, nint, int>)vtable[25];
            ThrowIfFailed(set(_factory, processId, (int)DataFlow.Render, (int)Role.Multimedia, hstring),
                "set multimedia route");
            ThrowIfFailed(set(_factory, processId, (int)DataFlow.Render, (int)Role.Console, hstring),
                "set console route");
        }
        finally
        {
            if (hstring != 0) _ = NativeMethods.WindowsDeleteString(hstring);
        }
    }

    private static string Pack(string endpointId) => $"{MmDevicePrefix}{endpointId}{RenderSuffix}";
    private static string Unpack(string endpointId)
    {
        if (endpointId.StartsWith(MmDevicePrefix, StringComparison.OrdinalIgnoreCase))
            endpointId = endpointId[MmDevicePrefix.Length..];
        if (endpointId.EndsWith(RenderSuffix, StringComparison.OrdinalIgnoreCase))
            endpointId = endpointId[..^RenderSuffix.Length];
        return endpointId;
    }

    private static void ThrowIfFailed(int hresult, string operation)
    {
        if (hresult < 0) throw new COMException($"Audio policy could not {operation}.", hresult);
    }

    private enum DataFlow { Render = 0, Capture = 1, All = 2 }
    private enum Role { Console = 0, Multimedia = 1, Communications = 2 }

    public void Dispose()
    {
        if (_factory == 0) return;
        Marshal.Release(_factory);
        _factory = 0;
        GC.SuppressFinalize(this);
    }

    ~AudioPolicyConfigAdapter() { if (_factory != 0) Marshal.Release(_factory); }

    private static class NativeMethods
    {
        [DllImport("combase.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        internal static extern int WindowsCreateString(string sourceString, uint length, out nint hstring);

        [DllImport("combase.dll", PreserveSig = true)]
        internal static extern int WindowsDeleteString(nint hstring);

        [DllImport("combase.dll", PreserveSig = true)]
        internal static extern nint WindowsGetStringRawBuffer(nint hstring, out uint length);

        [DllImport("combase.dll", PreserveSig = true)]
        internal static extern int RoGetActivationFactory(nint activatableClassId, [In] ref Guid iid,
            out nint factory);
    }
}
