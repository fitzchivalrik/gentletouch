using System.Runtime.InteropServices;

namespace GentleTouch.Interop.DualSense;

[StructLayout(LayoutKind.Explicit, Size = SizeUsb)]
internal struct InputReport
{
    internal const byte SizeUsb       = 0x40;
    internal const byte SizeBluetooth = 0x4E;
    internal const byte IdUsb         = 0x01;
    internal const byte IdBluetooth   = 0x31;

    [FieldOffset(0x00)] internal byte Id;

    // Skip over all the stuff currently not needed
    [FieldOffset(0x05)] internal byte L2;
    [FieldOffset(0x06)] internal byte R2;
    [FieldOffset(0x07)] internal byte SeqNumber;
    [FieldOffset(0x08)] internal byte Buttons0;
    [FieldOffset(0x09)] internal byte Buttons1;
    [FieldOffset(0x0A)] internal byte Buttons2;
    [FieldOffset(0x0B)] internal byte Buttons3;
}