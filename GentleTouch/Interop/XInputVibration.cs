using System.Runtime.InteropServices;

namespace GentleTouch.Interop
{
    [StructLayout(LayoutKind.Explicit, Size = 0x4)]
    public readonly struct XInputVibration
    {
        [FieldOffset(0x0)] public readonly ushort WLeftMotorSpeed;
        [FieldOffset(0x2)] public readonly ushort WRightMotorSpeed;

        public XInputVibration(ushort wLeftMotorSpeed, ushort wRightMotorSpeed)
        {
            WLeftMotorSpeed = wLeftMotorSpeed;
            WRightMotorSpeed = wRightMotorSpeed;
        }
    }


}