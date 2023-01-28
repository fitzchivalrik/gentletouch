using System;

namespace GentleTouch.Interop.DualSense;

[Flags]
public enum Buttons1 : byte
{
    None    = 0
  , L1      = 1 << 0
  , R1      = 1 << 1
  , L2      = 1 << 2
  , R2      = 1 << 3
  , Create  = 1 << 4 // Share/Select
  , Options = 1 << 5 // Start
  , L3      = 1 << 6
  , R3      = 1 << 7
}

public enum Buttons2 : byte
{
    None     = 0
  , PsHome   = 1 << 0 // System
  , Touchpad = 1 << 1
  , MicMute  = 1 << 2
}