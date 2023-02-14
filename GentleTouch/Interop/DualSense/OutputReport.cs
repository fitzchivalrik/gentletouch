using System.Runtime.InteropServices;

namespace GentleTouch.Interop.DualSense;
// Primary sources:
// https://github.com/torvalds/linux/blob/master/drivers/hid/hid-playstation.c#L227
// &
// https://github.com/BadMagic100/DualSenseAPI/
// &
// https://github.com/nullkal/UniSense/

[StructLayout(LayoutKind.Explicit, Size = Size)]
internal unsafe struct OutputReportCommon
{
    internal const byte Size = 0x2F;

    internal const byte DS_OUTPUT_POWER_SAVE_CONTROL_MIC_MUTE = 1 << 4;
    internal const byte DS_OUTPUT_LIGHTBAR_SETUP_LIGHT_OUT    = 1 << 1;

    internal const byte FFXIV_FLAG1_DEFAULT = 0x43;

    [FieldOffset(0x00)] public byte Flag0;
    [FieldOffset(0x01)] public byte Flag1;

    [FieldOffset(0x02)] public       byte MotorRight;
    [FieldOffset(0x03)] public       byte MotorLeft;
    [FieldOffset(0x04)] public fixed byte Reserved[4];
    [FieldOffset(0x08)] public       byte MicButtonLed;
    [FieldOffset(0x09)] public       byte PowerSaveControl;

    [FieldOffset(0x0A)] public fixed byte TriggerR2[10];
    [FieldOffset(0x14)] public       byte Unk;
    [FieldOffset(0x15)] public fixed byte TriggerL2[10];
    [FieldOffset(0x1E)] public fixed byte Unk2[8];

    [FieldOffset(0x26)] public       byte Flag2;
    [FieldOffset(0x27)] public fixed byte Reserved3[2];
    [FieldOffset(0x29)] public       byte LightBarSetup;
    [FieldOffset(0x2A)] public       byte LedBrightness;

    [FieldOffset(0x2B)] public byte PlayerLeds;

    [FieldOffset(0x2C)] public byte LightBarColourRed;
    [FieldOffset(0x2D)] public byte LightBarColourGreen;
    [FieldOffset(0x2E)] public byte LightBarColourBlue;
}

[StructLayout(LayoutKind.Explicit, Size = Size)]
internal struct OutputReportUSB
{
    internal const byte Size = OutputReportCommon.Size + 0x01;

    // USB 0x02, BT ignored, different header and headache
    // TODO: BT support?
    internal const byte IdUsb = 0x02;

    // Must be ID_USB
    // TODO: How to set automatically, but without overhead of method call and memcopies
    [FieldOffset(0x00)] internal byte               Id;
    [FieldOffset(0x01)] internal OutputReportCommon reportCommon;
}