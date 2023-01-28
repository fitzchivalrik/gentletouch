namespace GentleTouch.Interop;

public static class Delegates
{
    // ReSharper disable once InconsistentNaming
    internal delegate void FFXIVSetState(nint maybeControllerStruct, int rightMotorSpeedPercent, int leftMotorSpeedPercent);

#if DEBUG
    private delegate int XInputWrapperSetState(int dwUserIndex, ref XInputVibration pVibration);
#endif

    internal delegate int MaybeControllerPoll(nint maybeControllerStruct);

    internal delegate byte WriteFileHidDOutputReport(int hidDevice, nuint outputReport, ushort reportLength);

    internal delegate nuint DeviceChangeDelegate(nuint inputDeviceManager);

    internal unsafe delegate nuint ParseRawInputReport(nuint unk1, byte* rawReport, nuint reportLength, byte unk4, nuint parseStructure);

    internal delegate byte DrawWeapon(nuint uiStateWeaponStateUnsheathed, bool isDrawn);
}