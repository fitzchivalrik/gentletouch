namespace GentleTouch.Interop.DualSense;

// Source: https://github.com/BadMagic100/DualSenseAPI/blob/master/DualSenseAPI/AdaptiveTrigger.cs
public enum AdaptiveTriggerEffectType : byte
{
    ContinuousResistance = 0x01
  , SectionResistance    = 0x02
  , Vibrate              = 0x26
  , Calibrate            = 0xFC
  , Default              = 0x00
}