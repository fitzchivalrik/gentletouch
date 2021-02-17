using System.Runtime.InteropServices;

namespace GentleTouch
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

    // NOTE (Chiv): Modified from
    // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/GameStructs/ActionManager.cs
    // Licensed under the AGPLv3 or later
    [StructLayout(LayoutKind.Explicit, Size = 0x14)]
    public readonly struct Cooldown
    {
        [FieldOffset(0x0)] public readonly byte IsCooldown;
        [FieldOffset(0x4)] public readonly uint ActionID;
        [FieldOffset(0x8)] public readonly float CooldownElapsed;
        [FieldOffset(0xC)] public readonly float CooldownTotal;

        public Cooldown(byte isCooldown, uint actionId, float cooldownElapsed, float cooldownTotal)
        {
            IsCooldown = isCooldown;
            ActionID = actionId;
            CooldownElapsed = cooldownElapsed;
            CooldownTotal = cooldownTotal;
        }

        public static implicit operator bool(Cooldown a)
        {
            return a.CooldownTotal - a.CooldownElapsed > 0.5f; //a.IsCooldown == 1; //&&
        }
    }
}