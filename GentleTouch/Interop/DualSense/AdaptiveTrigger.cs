namespace GentleTouch.Interop.DualSense;

// Source: https://github.com/BadMagic100/DualSenseAPI/blob/master/DualSenseAPI/AdaptiveTrigger.cs
public enum AdaptiveTriggerEffectType : byte
{
    Default              = 0x00
  , ContinuousResistance = 0x01
  , SectionResistance    = 0x02
  , Vibrate              = 0x26
  , Calibrate            = 0xFC
}