using System.Runtime.InteropServices;

namespace GentleTouch.Interop
{
    // NOTE (Chiv): Modified from
    // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/GameStructs/ActionManager.cs
    // Licensed under the AGPLv3 or later
    [StructLayout(LayoutKind.Explicit, Size = 0x14)]
    public readonly struct Cooldown
    {
        [FieldOffset(0x0)] public readonly byte IsActive;
        [FieldOffset(0x4)] public readonly uint ActionID;
        [FieldOffset(0x8)] public readonly float CooldownElapsed;
        [FieldOffset(0xC)] public readonly float CooldownTotal;

        public Cooldown(byte isActive, uint actionId, float cooldownElapsed, float cooldownTotal)
        {
            IsActive = isActive;
            ActionID = actionId;
            CooldownElapsed = cooldownElapsed;
            CooldownTotal = cooldownTotal;
        }

        public static implicit operator bool(Cooldown a)
        {
            // NOTE 0.5s seems to work even with ping > 500ms.
            // Eyeballing reaction time into it, presses should come at most ~0.35s before
            // end, which works for queuing.
            return a.CooldownTotal - a.CooldownElapsed > 0.5f; //a.IsCooldown == 1; //&&
        }
    }
}