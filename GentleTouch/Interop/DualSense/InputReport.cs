using System.Runtime.InteropServices;

namespace GentleTouch.Interop.DualSense;

[StructLayout(LayoutKind.Explicit, Size = Size)]
internal struct InputReport
{
    internal const byte Size = 0x40;

    [FieldOffset(0x00)] internal byte Id;

    // Skip over all the stuff currently not needed
    [FieldOffset(0x05)] internal byte L2;
    [FieldOffset(0x06)] internal byte R2;
    [FieldOffset(0x07)] internal byte SeqNumber;
    [FieldOffset(0x08)] internal byte Button0;
    [FieldOffset(0x09)] internal byte Button1;
    [FieldOffset(0x0A)] internal byte Button2;
    [FieldOffset(0x0B)] internal byte Button3;
}