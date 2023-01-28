using System;

namespace GentleTouch.Interop.DualSense;

[Flags]
internal enum OutputReportFlag0 : byte
{
    None                   = 0
  , CompatibleVibration    = 1 << 0
  , HapticsSelect          = 1 << 1
  , AdapterTriggerR2Select = 1 << 2
  , AdapterTriggerL2Select = 1 << 3
}

[Flags]
internal enum OutputReportFlag1 : byte
{
    None                         = 0
  , MicMuteLedControlEnable      = 1 << 0
  , PowerSaveControlEnable       = 1 << 1
  , LightbarControlEnable        = 1 << 2
  , ReleaseLeds                  = 1 << 3
  , PlayerIndicatorControlEnable = 1 << 4
}

[Flags]
internal enum OutputReportFlag2 : byte
{
    None                       = 0
  , LightbarSetupControlEnable = 1 << 1
  , CompatibleVibration2       = 1 << 2
}