using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using GentleTouch.Interop;
using GentleTouch.Interop.DualSense;
using GentleTouch.Triggers;
using GentleTouch.UI;
using Lumina.Excel.GeneratedSheets;
using SharpDX.DirectInput;
using Condition = Dalamud.Game.ClientState.Conditions.Condition;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace GentleTouch;

// TODO: Holy, does this need a refactor.
public class GentleTouch : IDalamudPlugin
{
    private const string Command    = "/gentle";
    public const  string PluginName = "GentleTouch";
    public        string Name => PluginName;

    private const string WriteFileHidDeviceReportSignature = "E8 ?? ?? ?? ?? 83 7B 18 00 74 55";

    //NOTE (Chiv) RowId of ClassJob sheet
    private static readonly HashSet<uint> JobsWhitelist = new()
    {
        19, 20, 21, 22, 23, 24, 25, 27, 28, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40
    };

    //NOTE (Chiv) RowId of ClassJobCategory sheet
    private static readonly HashSet<uint> JobCategoryWhitelist = new()
    {
        20, 21, 22, 23, 24, 25, 26, 28, 29, 92, 96, 98, 99, 111, 112, 129, 149, 150, 180, 181
    };

    [Signature(WriteFileHidDeviceReportSignature)]
    private readonly Delegates.WriteFileHidDOutputReport _writeFileHidDOutputReport = null!;

    // Alternative
    //    "40 ?? 53 56 48 8B ?? 48 81 EC"; //40 56 57 41 56 48 81 EC ? ? ? ? 44 0F 29 84 24 ? ? ? ?
    [Signature("40 55 53 56 48 8b ec 48 81 ec 80 00 00 00 33 f6 44 8b d2 4c 8b c9")]
    private readonly Delegates.FFXIVSetState _ffxivSetState = null!;

#if DEBUG
    [Signature("E8 ?? ?? ?? ?? 48 81 C4 ?? ?? ?? ?? 5E 5B 5D C3 49 8B 9A")]
    private readonly XInputWrapperSetState _xInputWrapperSetState = null!;
#endif

    // Alternative
    // 40 56 57 41 56 48 81 EC ?? ?? ?? ?? 44 0F 29 84 24 ?? ?? ?? ??
    [Signature("40 ?? 57 41 ?? 48 81 EC ?? ?? ?? ?? 44 0F ?? ?? ?? ?? ?? ?? ?? 48 8B", DetourName = nameof(ControllerPollDetour))]
    private readonly Hook<Delegates.MaybeControllerPoll> _controllerPoll = null!;

    [Signature(WriteFileHidDeviceReportSignature, DetourName = nameof(WriteFileHidDOutputReportDetour))]
    private readonly Hook<Delegates.WriteFileHidDOutputReport> _writeFileHidDOutputReportHook = null!;

    [Signature("48 83 EC 38 0F B6 81", DetourName = nameof(DeviceChangeDetour))]
    private readonly Hook<Delegates.DeviceChangeDelegate> _deviceChangeHook = null!;

    private Hook<Delegates.ParseRawInputReport>? _parseRawInputReportHook;

    private readonly nint _parseRawDualShock4InputReportAddress;
    private readonly nint _parseRawDualSenseInputReportAddress;

    private readonly DalamudPluginInterface              _pluginInterface;
    private readonly ClientState                         _clientState;
    private readonly ObjectTable                         _objects;
    private readonly Framework                           _framework;
    private readonly CommandManager                      _commands;
    private readonly Condition                           _condition;
    private readonly IReadOnlyCollection<ClassJob>       _jobs;
    private readonly IReadOnlyCollection<FFXIVAction>    _allActions;
    private readonly PriorityQueue<CooldownTrigger, int> _queue = new();
    private readonly Configuration                       _config;
    private readonly AetherCurrentTrigger                _aetherCurrentTrigger;
    private readonly Guid                                _dualSenseGuid = Guid.Parse("0ce6054c-0000-0000-0000-504944564944");
    private readonly DirectInput                         _directInput   = new();

    private IEnumerator<VibrationPattern.Step?>? _currentEnumerator;
    private VibrationTrigger?                    _highestPriorityTrigger;

    // Init in method called from constructor
    private Action<int, int> _setControllerState = null!;

    private nint _maybeControllerStruct;
    private bool _shouldDrawConfigUi;
    private bool _isDisposed;
    private bool _noGamepadAttached;
    private bool _isDualSense;
    private int  _hidDevice = 1;
    private byte _dualSenseOutputReportFlag0;
    private byte _dualSenseOutputReportFlag2;

#if DEBUG
        private int _rightMotorSpeed = 100;
        private int _leftMotorSpeed;
        private int _dwControllerIndex = 1;
        private int _cooldownGroup = CooldownTrigger.GCDCooldownGroup;
        private readonly int[] _lastReturnedFromPoll = new int[100];
        private int _currentIndex;
#endif

    public GentleTouch(
        DalamudPluginInterface pi
      , SigScanner             sigScanner
      , ClientState            clientState
      , DataManager            data
      , ObjectTable            objects
      , CommandManager         commands
      , Framework              framework
      , Condition              condition
    )
    {
        var config = pi.GetPluginConfig() as Configuration ?? new Configuration();
        _pluginInterface = pi;
        _clientState     = clientState;
        _commands        = commands;
        _objects         = objects;
        _framework       = framework;
        _condition       = condition;
        _config          = config;
        // NOTE (Chiv) Resolve pattern GUIDs to pattern
        // TODO (Chiv) Better in custom Serializer?
        foreach (var trigger in _config.CooldownTriggers)
        {
            trigger.Pattern = _config.Patterns.FirstOrDefault(p => p.Guid == trigger.PatternGuid) ??
                              new VibrationPattern();
        }

        _clientState.Login  += OnLogin;
        _clientState.Logout += OnLogout;

        #region Hooks, Functions and Addresses

        SignatureHelper.Initialise(this);

        _parseRawDualSenseInputReportAddress  = sigScanner.ScanText("E8 ?? ?? ?? ?? 8B 4B 08 48 8D 55 A0");
        _parseRawDualShock4InputReportAddress = sigScanner.ScanText("E8 ?? ?? ?? ?? EB 2C 0F B7 53 22");
        _controllerPoll.Enable();
        _writeFileHidDOutputReportHook.Enable();
        _deviceChangeHook.Enable();

        #endregion

        #region Excel Data

        //TODO Handle potential nulls....so far it has not thrown...
        _jobs = data.Excel.GetSheet<ClassJob>()
                    .Where(j => JobsWhitelist.Contains(j.RowId))
                    .ToArray();
        var actions = data.Excel.GetSheet<FFXIVAction>()
                          .Where(a => a.IsPlayerAction && a.CooldownGroup != CooldownTrigger.GCDCooldownGroup &&
                                      !a.IsPvP);
        var gcdActions = data.Excel.GetSheet<FFXIVAction>()
                             .Where(a =>
                                  a.IsPlayerAction && !a.IsPvP && a.CooldownGroup == CooldownTrigger.GCDCooldownGroup
                                  && !a.IsRoleAction && JobCategoryWhitelist.Contains(a.ClassJobCategory.Row))
                             .GroupBy(
                                  a => a.ClassJobCategory.Row,
                                  (_, group) => group.First()
                              );
        var allActions = actions.Concat(gcdActions);
        _allActions = allActions as FFXIVAction[] ?? allActions.ToArray();

        #endregion

        CheckForGamepads();
        _aetherCurrentTrigger = AetherCurrentTrigger.CreateAetherCurrentTrigger(
            () => _config.MaxAetherCurrentSenseDistanceSquared, _objects);
        _commands.AddHandler(Command, new CommandInfo((cmd, args) =>
        {
            if (args == "r")
            {
                PluginLog.Debug("Manual refresh of DualSense state.");
                UpdateDualSenseState();
            } else
            {
                OnOpenConfigUi();
            }
        })
        {
            HelpMessage = "Open GentleTouch configuration menu.", ShowInHelp = true
        });

        UpdateDualSenseState();

        // NOTE (Chiv) LocalPlayer != null => logged in, but plugin is just loading => do login logic
        if (_clientState.LocalPlayer is not null)
        {
            OnLogin(null, null!);
        }
    }

    private void CheckForGamepads()
    {
        var devices = _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);
        foreach (var device in devices)
        {
            PluginLog.Debug($"Device\n{device.ProductName}:{device.ProductGuid}");
        }

        _isDualSense = devices.Any(d => d.ProductGuid == _dualSenseGuid);
        PluginLog.Debug($"Is DualSense? {_isDualSense}");
        PluginLog.Debug($"No GamePad Attached? {devices.Count == 0}");
        _noGamepadAttached  = devices.Count == 0;
        _setControllerState = _isDualSense ? DualSenseSetState : DefaultControllerSetState;

        _parseRawInputReportHook?.Disable();
        _parseRawInputReportHook?.Dispose();
        _parseRawInputReportHook = null;
        unsafe
        {
            _parseRawInputReportHook =
                Hook<Delegates.ParseRawInputReport>
                   .FromAddress(
                        _isDualSense ? _parseRawDualSenseInputReportAddress : _parseRawDualShock4InputReportAddress,
                        _isDualSense ? ParseDualSenseRawInputReportDetour : ParseDualShock4RawInputReportDetour
                    );
        }

        _parseRawInputReportHook.Enable();
    }

    private void OnLogout(object? sender, EventArgs e)
    {
        _pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        _pluginInterface.UiBuilder.Draw         -= DrawUi;
        _framework.Update                       -= FrameworkOutOfCombatUpdate;
        _framework.Update                       -= FrameworkInCombatUpdate;
        if (!_noGamepadAttached) _setControllerState(0, 0);
        _currentEnumerator?.Dispose();
        _currentEnumerator      = null;
        _highestPriorityTrigger = null;
        _queue.Clear();
    }

    private void OnLogin(object? sender, EventArgs e)
    {
        _pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        _pluginInterface.UiBuilder.Draw         += DrawUi;
        _framework.Update                       += FrameworkOutOfCombatUpdate;
    }

    private void FrameworkInCombatUpdate(Framework framework)
    {
        if (!_condition[ConditionFlag.InCombat]
            || _condition[ConditionFlag.Unconscious]
            || _condition[ConditionFlag.WatchingCutscene]
            || _condition[ConditionFlag.WatchingCutscene78]
            || _condition[ConditionFlag.OccupiedInCutSceneEvent]
            || _noGamepadAttached)
        {
            ResetQueueAndTriggers();
            _framework.Update += FrameworkOutOfCombatUpdate;
            _framework.Update -= FrameworkInCombatUpdate;
            return;
        }

        // We are in combat, it will not be Null
        var localPlayer = (_objects[0] as PlayerCharacter)!;
        var weaponSheathed = _config.NoVibrationWithSheathedWeapon &&
                             !localPlayer!.IsStatus(StatusFlags.WeaponOut);
        if (weaponSheathed)
        {
            _setControllerState(0, 0);
            _framework.Update += FrameworkInCombatPauseUpdate;
            _framework.Update -= FrameworkInCombatUpdate;
            return;
        }

        var casting = _config.NoVibrationDuringCasting &&
                      localPlayer.IsStatus(StatusFlags.IsCasting);
        if (casting)
        {
            _setControllerState(0, 0);
            _framework.Update += FrameworkInCombatPauseUpdate;
            _framework.Update -= FrameworkInCombatUpdate;
            return;
        }

        EnqueueCooldownTriggers();
        UpdateHighestPriorityTrigger();
        CheckAndVibrate();
    }

    private void FrameworkInCombatPauseUpdate(Framework framework)
    {
        if (!_condition[ConditionFlag.InCombat]
            || _condition[ConditionFlag.Unconscious]
            || _condition[ConditionFlag.WatchingCutscene]
            || _condition[ConditionFlag.WatchingCutscene78]
            || _condition[ConditionFlag.OccupiedInCutSceneEvent]
            || _noGamepadAttached)
        {
            ResetQueueAndTriggers();
            _framework.Update += FrameworkOutOfCombatUpdate;
            _framework.Update -= FrameworkInCombatPauseUpdate;
        }

        // We are in combat, it will not be Null
        var localPlayer = (_objects[0] as PlayerCharacter)!;
        var weaponSheathed = _config.NoVibrationWithSheathedWeapon &&
                             !localPlayer.IsStatus(StatusFlags.WeaponOut);
        if (weaponSheathed)
        {
            return;
        }

        var casting = _config.NoVibrationDuringCasting &&
                      localPlayer.IsStatus(StatusFlags.IsCasting);
        if (casting)
        {
            return;
        }

        _framework.Update += FrameworkInCombatUpdate;
        _framework.Update -= FrameworkInCombatPauseUpdate;
    }

    private void ResetQueueAndTriggers()
    {
        _queue.Clear();
        _highestPriorityTrigger = null;
        _currentEnumerator?.Dispose();
        _currentEnumerator = null;
        foreach (var ct in _config.CooldownTriggers)
        {
            ct.ShouldBeTriggered = false;
        }

        _setControllerState(0, 0);
    }

    private void CheckAndVibrate()
    {
        if (_currentEnumerator is null) return;
        if (_currentEnumerator.MoveNext())
        {
            var s = _currentEnumerator.Current;
            if (s is not null)
            {
                _setControllerState(s.LeftMotorPercentage, s.RightMotorPercentage);
            }
        } else
        {
            _setControllerState(0, 0);
            _currentEnumerator.Dispose();
            _currentEnumerator      = null;
            _highestPriorityTrigger = null;
        }
    }

    private void UpdateHighestPriorityTrigger()
    {
        if (!_queue.TryPeek(out _, out var priority)) return;
        if ((_highestPriorityTrigger?.Priority ?? int.MaxValue) <= priority) return;
        if (_highestPriorityTrigger?.Pattern.Infinite ?? false) _highestPriorityTrigger.ShouldBeTriggered = true;
        _currentEnumerator?.Dispose();
        _highestPriorityTrigger = _queue.Dequeue();
        _currentEnumerator      = _highestPriorityTrigger.GetEnumerator();
        _setControllerState(0, 0);
    }

    private void EnqueueCooldownTriggers()
    {
        // NOTE: This only happens in combat, so the player must exist
        var localPlayer  = (_objects[0] as PlayerCharacter)!;
        var currentJobId = localPlayer.ClassJob.Id;

        foreach (var t in _config.CooldownTriggers)
        {
            if (t.JobId != currentJobId) continue;
            var elapsed = 0.0f;
            var total   = 0.0f;
            unsafe
            {
                var c = ActionManager.Instance()->GetRecastGroupDetail(
                    t.ActionCooldownGroup - 1);
                total   = c->Total / ActionManager.GetMaxCharges(t.ActionId, localPlayer.Level);
                elapsed = c->Elapsed;
            }
            // Check for all triggers _in_ cooldown state and set ShouldBeTriggered to true
            // -> We want them to be triggered when leaving the cooldown state!

            // NOTE 0.5s seems to work even with ping > 500ms.
            // Eyeballing reaction time into it, presses should come at most ~0.35s before
            // end, which works for queuing.

            if (total - MathF.Min(elapsed, total) > 0.5f)
            {
                if (_highestPriorityTrigger == t)
                {
                    // NOTE: Memory leak if not disposed, should exist if _highestPriorityTrigger is set
                    _currentEnumerator!.Dispose();
                    _currentEnumerator      = null;
                    _highestPriorityTrigger = null;
                    _setControllerState(0, 0);
                }

                t.ShouldBeTriggered = true;
            }
            // Check for all triggers _not_ in cooldown state. If they ShouldBeTriggered (meaning, there were in 
            // cooldown state prior), add them to the queue.
            else if (t.ShouldBeTriggered)
            {
                t.ShouldBeTriggered = false;
                _queue.Enqueue(t, t.Priority);
            }
        }
    }

    private void FrameworkOutOfCombatUpdate(Framework framework)
    {
        if (_condition[ConditionFlag.Unconscious]
            || _condition[ConditionFlag.WatchingCutscene]
            || _condition[ConditionFlag.WatchingCutscene78]
            || _condition[ConditionFlag.OccupiedInCutSceneEvent]
            || _noGamepadAttached)
        {
            return;
        }

        var inCombat = _condition[ConditionFlag.InCombat];
        if (!inCombat)
        {
            switch (_config.SenseAetherCurrents)
            {
                case true when _currentEnumerator is null:
                    _currentEnumerator = _aetherCurrentTrigger.GetEnumerator();
                    break;
                case false when _currentEnumerator is not null && _currentEnumerator == _aetherCurrentTrigger.GetEnumerator():
                    _currentEnumerator = null;
                    _setControllerState(0, 0);
                    break;
            }

            // NOTE (Chiv) Invariant: Combat triggers are cleared
            CheckAndVibrate();
            return;
        }

        if (_objects[0] is not PlayerCharacter localPlayer)
        {
            return;
        }

        var weaponSheathed = _config.NoVibrationWithSheathedWeapon &&
                             !localPlayer.IsStatus(StatusFlags.WeaponOut);
        if (weaponSheathed) return;
        var casting = _config.NoVibrationDuringCasting &&
                      localPlayer.IsStatus(StatusFlags.IsCasting);
        if (casting) return;
        _currentEnumerator = null;
        _setControllerState(0, 0);
        _framework.Update += FrameworkInCombatUpdate;
        _framework.Update -= FrameworkOutOfCombatUpdate;
    }

    private void DualSenseSetState(int leftMotorPercentage, int rightMotorPercentage)
    {
        // Invariant: Dualsense connection already checked
        unsafe
        {
            var isReset = rightMotorPercentage == 0 && leftMotorPercentage == 0;
            var report  = stackalloc OutputReportUSB[1];
            report->Id = OutputReportUSB.IdUsb;
            // TODO: Monitor, if this is correct, or maybe the haptic select must always be on?
            report->reportCommon.Flag0      = isReset ? (byte)0x00 : _dualSenseOutputReportFlag0;
            report->reportCommon.Flag1      = 0x00;
            report->reportCommon.MotorRight = (byte)(0xFF * (rightMotorPercentage / 100f));
            report->reportCommon.MotorLeft  = (byte)(0xFF * (leftMotorPercentage / 100f));
            report->reportCommon.Flag2      = isReset ? (byte)0x00 : _dualSenseOutputReportFlag2;
            _writeFileHidDOutputReport(_hidDevice, (nuint)report, OutputReportUSB.Size);
        }
    }

    private void DefaultControllerSetState(int leftMotorPercentage, int rightMotorPercentage)
    {
#if DEBUG
            PluginLog.Verbose(
                $"Setting controller state to L: {leftMotorPercentage}, R: {rightMotorPercentage}, direct? {direct}, Index: {dwControllerIndex}");

            if (direct)
            {
                var t = new XInputVibration((ushort) (leftMotorPercentage / 100.0 * ushort.MaxValue),
                    (ushort) (rightMotorPercentage / 100.0 * ushort.MaxValue));
                _xInputWrapperSetState(dwControllerIndex, ref t);
            }
            else
#endif
        {
            _ffxivSetState(_maybeControllerStruct, rightMotorPercentage, leftMotorPercentage);
        }
    }

    private void UpdateDualSenseState()
    {
        if (!_isDualSense) return;
        if (_config.LegacyDualSenseVibrations)
        {
            _dualSenseOutputReportFlag0 = (byte)(OutputReportFlag0.HapticsSelect | OutputReportFlag0.CompatibleVibration);
            _dualSenseOutputReportFlag2 = 0x00;
        } else
        {
            _dualSenseOutputReportFlag0 = (byte)OutputReportFlag0.HapticsSelect;
            _dualSenseOutputReportFlag2 = (byte)OutputReportFlag2.CompatibleVibration2;
        }

        unsafe
        {
            var report = stackalloc OutputReportUSB[1];
            report->Id = OutputReportUSB.IdUsb;
            if (_config.TurnLightBarOn)
            {
                report->reportCommon.Flag1               = (byte)OutputReportFlag1.LightbarControlEnable;
                report->reportCommon.LightBarColourRed   = (byte)_config.LightBarColour.X;
                report->reportCommon.LightBarColourGreen = (byte)_config.LightBarColour.Y;
                report->reportCommon.LightBarColourBlue  = (byte)_config.LightBarColour.Z;
            }

            if (_config.SetDualSenseAdaptiveTrigger)
            {
#if DEBUG
                PluginLog.Debug($"TriggerL2 Position {(byte)(_config.TriggerL2StartPosition / 100f * byte.MaxValue)}," +
                                $" TriggerL2 Force {(byte)(_config.TriggerL2StartForce / 100f * byte.MaxValue)}");
                PluginLog.Debug($"TriggerR2 Position {(byte)(_config.TriggerR2StartPosition / 100f * byte.MaxValue)}," +
                                $" TriggerR2 Force {(byte)(_config.TriggerR2StartForce / 100f * byte.MaxValue)}");
#endif
                report->reportCommon.Flag0        = (byte)(OutputReportFlag0.AdapterTriggerR2Select | OutputReportFlag0.AdapterTriggerL2Select);
                report->reportCommon.TriggerL2[0] = (byte)_config.DualSenseAdaptiveTriggerType;
                report->reportCommon.TriggerL2[1] = (byte)(_config.TriggerL2StartPosition / 100f * byte.MaxValue);
                report->reportCommon.TriggerL2[2] = (byte)(_config.TriggerL2StartForce / 100f * byte.MaxValue);
                report->reportCommon.TriggerR2[0] = (byte)_config.DualSenseAdaptiveTriggerType;
                report->reportCommon.TriggerR2[1] = (byte)(_config.TriggerR2StartPosition / 100f * byte.MaxValue);
                report->reportCommon.TriggerR2[2] = (byte)(_config.TriggerR2StartForce / 100f * byte.MaxValue);
            } else
            {
                report->reportCommon.Flag0        = (byte)(OutputReportFlag0.AdapterTriggerR2Select | OutputReportFlag0.AdapterTriggerL2Select);
                report->reportCommon.TriggerL2[0] = (byte)AdaptiveTriggerEffectType.Default;
                report->reportCommon.TriggerR2[0] = (byte)AdaptiveTriggerEffectType.Default;
            }

            _writeFileHidDOutputReport(_hidDevice, (nuint)report, OutputReportUSB.Size);
        }
    }

    #region Detour

    private nuint DeviceChangeDetour(nuint inputDeviceManager)
    {
        CheckForGamepads();
        return _deviceChangeHook.Original(inputDeviceManager);
    }

    private int ControllerPollDetour(nint maybeControllerStruct)
    {
        _maybeControllerStruct = maybeControllerStruct;
        _controllerPoll.Disable();
        return _controllerPoll.Original(maybeControllerStruct);
    }

    private byte WriteFileHidDOutputReportDetour(int hidDevice, nuint outputReport, ushort reportLength)
    {
        _hidDevice = hidDevice;
        return _writeFileHidDOutputReportHook.Original(hidDevice, outputReport, reportLength);
    }

    private unsafe nuint ParseDualSenseRawInputReportDetour(nuint unk1, byte* rawReport, nuint unk3, byte unk4, nuint parseStructure)
    {
        var report = (InputReport*)rawReport;
        if ((report->Button1 & (byte)Buttons1.Create) > 0)
        {
            var macro    = RaptureMacroModule.Instance->Individual[96];
            var instance = RaptureShellModule.Instance;
            if (macro != null && !instance->MacroLocked)
            {
                RaptureShellModule.Instance->ExecuteMacro(macro);
            }
        }

        if ((report->Button2 & (byte)Buttons2.PsHome) > 0)
        {
            var macro    = RaptureMacroModule.Instance->Individual[97];
            var instance = RaptureShellModule.Instance;
            if (macro != null && !instance->MacroLocked)
            {
                RaptureShellModule.Instance->ExecuteMacro(macro);
            }
        }

        //The detour is only called if the hook is set.
        var result = _parseRawInputReportHook!.Original(unk1, rawReport, unk3, unk4, parseStructure);
        return result;
    }

    private unsafe nuint ParseDualShock4RawInputReportDetour(nuint unk1, byte* rawReport, nuint reportLength, byte unk4, nuint parseStructure)
    {
        var report = (Interop.DualShock4.InputReport*)rawReport;
        if ((report->Button2 & (byte)Buttons2.Touchpad) > 0)
        {
            var macro    = RaptureMacroModule.Instance->Individual[96];
            var instance = RaptureShellModule.Instance;
            if (macro != null && !instance->MacroLocked)
            {
                RaptureShellModule.Instance->ExecuteMacro(macro);
            }
        }

        if ((report->Button2 & (byte)Buttons2.PsHome) > 0)
        {
            var macro    = RaptureMacroModule.Instance->Individual[97];
            var instance = RaptureShellModule.Instance;
            if (macro != null && !instance->MacroLocked)
            {
                RaptureShellModule.Instance->ExecuteMacro(macro);
            }
        }

        //The detour is only called if the hook is set.
        var result = _parseRawInputReportHook!.Original(unk1, rawReport, reportLength, unk4, parseStructure);
        return result;
    }

    #endregion

    #region UI

    private void DrawUi()
    {
        // TODO (Chiv) Refactor to add/remove UI Event instead of boolean
#if DEBUG
            DrawDebugUi();
#endif
        if (!_shouldDrawConfigUi) return;
        (_shouldDrawConfigUi, var changed) = Config.DrawConfigUi(_config, _pluginInterface, _jobs, _allActions, ref _currentEnumerator);
        if (!changed) return;
#if DEBUG
                PluginLog.Verbose("Config changed, saving...");
#endif
        _pluginInterface.SavePluginConfig(_config);
        UpdateDualSenseState();
    }

    private void OnOpenConfigUi()
    {
        _shouldDrawConfigUi = !_shouldDrawConfigUi;
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        if (disposing)
        {
            // TODO (Chiv) Still not quite sure about correct dispose
            // NOTE (Chiv) Explicit, non GC? call - remove managed thingies too.
            OnLogout(null!, null!);
            _clientState.Login  -= OnLogin;
            _clientState.Logout -= OnLogout;
            _commands.RemoveHandler(Command);
        }

        // NOTE (Chiv) Implicit, GC? call and explicit, non GC? call - remove unmanaged thingies.
        if (_controllerPoll.IsEnabled) _controllerPoll.Disable();
        _controllerPoll.Dispose();
        if (_writeFileHidDOutputReportHook.IsEnabled) _writeFileHidDOutputReportHook.Disable();
        _writeFileHidDOutputReportHook.Dispose();
        if (_deviceChangeHook.IsEnabled) _deviceChangeHook.Disable();
        _deviceChangeHook.Dispose();
        if (_parseRawInputReportHook?.IsEnabled ?? false) _parseRawInputReportHook?.Disable();
        _parseRawInputReportHook?.Dispose();

        _isDisposed = true;
    }

    ~GentleTouch()
    {
        Dispose(false);
    }

    #endregion
}