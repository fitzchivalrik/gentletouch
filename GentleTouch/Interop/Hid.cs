#if DEBUG
// Maybe sometime, but not now, leaving for reference
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GentleTouch.Interop;

internal static class Hid
{
    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out nint pointerToPreparsedData);

    [DllImport("hid.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool HidD_GetManufacturerString(SafeFileHandle hidDeviceObject, nint pointerToBuffer, uint bufferLength);

    [DllImport("hid.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, nint pointerToBuffer, uint bufferLength);

    [DllImport("hid.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
    private static extern bool HidD_GetSerialNumberString(SafeFileHandle hidDeviceObject, nint pointerToBuffer, uint bufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern int HidP_GetCaps(nint pointerToPreparsedData, out HidCollectionCapabilities hidCollectionCapabilities);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, out HidAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_FreePreparsedData(nint pointerToPreparsedData);
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct HidAttributes
{
    internal readonly uint   Size;
    internal readonly ushort VendorId;
    internal readonly ushort ProductId;
    internal readonly ushort VersionNumber;
}

// https://www.pinvoke.net/default.aspx/hid.HIDP_CAPS
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct HidCollectionCapabilities
{
    private readonly ushort Usage;
    private readonly ushort UsagePage;
    private readonly ushort InputReportByteLength;
    private readonly ushort OutputReportByteLength;
    private readonly ushort FeatureReportByteLength;
    private fixed    ushort Reserved[17];
    private readonly ushort NumberLinkCollectionNodes;
    private readonly ushort NumberInputButtonCaps;
    private readonly ushort NumberInputValueCaps;
    private readonly ushort NumberInputDataIndices;
    private readonly ushort NumberOutputButtonCaps;
    private readonly ushort NumberOutputValueCaps;
    private readonly ushort NumberOutputDataIndices;
    private readonly ushort NumberFeatureButtonCaps;
    private readonly ushort NumberFeatureValueCaps;
    private readonly ushort NumberFeatureDataIndices;
}
#endif