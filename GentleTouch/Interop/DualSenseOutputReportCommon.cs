using System.Runtime.InteropServices;

namespace GentleTouch.Interop;

// https://github.com/torvalds/linux/blob/master/drivers/hid/hid-playstation.c#L227
// Adaptive Trigger:
// https://github.com/BadMagic100/DualSenseAPI/blob/master/DualSenseAPI/State/DualSenseOutputState.cs
[StructLayout(LayoutKind.Explicit, Size = SIZE)]
internal unsafe struct DualSenseOutputReportCommon
{
    internal const byte SIZE = 0x2F;

    internal const byte DS_OUTPUT_VALID_FLAG0_COMPATIBLE_VIBRATION    = 1 << 0;
    internal const byte DS_OUTPUT_VALID_FLAG0_HAPTICS_SELECT          = 1 << 1;
    internal const byte DS_OUTPUT_VALID_FLAG0_ADAPTIVE_TRIGGER_SELECT = 1 << 2;

    internal const byte DS_OUTPUT_VALID_FLAG1_MIC_MUTE_LED_CONTROL_ENABLE     = 1 << 0;
    internal const byte DS_OUTPUT_VALID_FLAG1_POWER_SAVE_CONTROL_ENABLE       = 1 << 1;
    internal const byte DS_OUTPUT_VALID_FLAG1_LIGHTBAR_CONTROL_ENABLE         = 1 << 2;
    internal const byte DS_OUTPUT_VALID_FLAG1_RELEASE_LEDS                    = 1 << 3;
    internal const byte DS_OUTPUT_VALID_FLAG1_PLAYER_INDICATOR_CONTROL_ENABLE = 1 << 4;

    internal const byte DS_OUTPUT_VALID_FLAG2_LIGHTBAR_SETUP_CONTROL_ENABLE = 1 << 1;
    internal const byte DS_OUTPUT_VALID_FLAG2_COMPATIBLE_VIBRATION2         = 1 << 2;
    internal const byte DS_OUTPUT_POWER_SAVE_CONTROL_MIC_MUTE               = 1 << 4;
    internal const byte DS_OUTPUT_LIGHTBAR_SETUP_LIGHT_OUT                  = 1 << 1;

    internal const byte FFXIV_FLAG1_DEFAULT = 0x43;

    [FieldOffset(0x00)] public byte Flag0;
    [FieldOffset(0x01)] public byte Flag1;

    [FieldOffset(0x02)] public       byte MotorRight;
    [FieldOffset(0x03)] public       byte MotorLeft;
    [FieldOffset(0x04)] public fixed byte Reserved[4];
    [FieldOffset(0x08)] public       byte MicButtonLed;
    [FieldOffset(0x09)] public       byte PowerSaveControl;

    [FieldOffset(0x0A)] public fixed byte TriggerR2[10];
    [FieldOffset(0x14)] public fixed byte TriggerL2[10];
    [FieldOffset(0x1E)] public fixed byte Reserved2[8];

    [FieldOffset(0x26)] public       byte Flag2; // LightBar?
    [FieldOffset(0x27)] public fixed byte Reserved3[2];
    [FieldOffset(0x29)] public       byte LightBarSetup;

    [FieldOffset(0x2A)] public byte PlayerLedBrightness;
    [FieldOffset(0x2B)] public byte PlayerLeds;

    [FieldOffset(0x2C)] public byte LightBarColourRed;
    [FieldOffset(0x2D)] public byte LightBarColourGreen;
    [FieldOffset(0x2E)] public byte LightBarColourBlue;
}

[StructLayout(LayoutKind.Explicit, Size = SIZE)]
internal struct DualSenseOutputReportUSB
{
    internal const byte SIZE = 0x30;

    // USB 0x02, BT ignored, different header and headache
    // TODO: BT support
    internal const byte ID_USB = 0x02;

    // Must be ID_USB
    // TODO: How to set automatically, but without overhead of method call and memcopies
    [FieldOffset(0x00)] internal byte                        Id;
    [FieldOffset(0x01)] internal DualSenseOutputReportCommon reportCommon;
}