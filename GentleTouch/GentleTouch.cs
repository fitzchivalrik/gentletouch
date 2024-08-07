﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using Device.Net;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using FFXIVClientStructs.Interop;
using GentleTouch.Interop;
using GentleTouch.Interop.DualSense;
using GentleTouch.Triggers;
using GentleTouch.UI;
using Hid.Net.Windows;
using Lumina.Excel.GeneratedSheets;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace GentleTouch;

// TODO: Holy, does this need a refactor.
public partial class GentleTouch : IDalamudPlugin
{
    public const  string     PluginName = "GentleTouch";
    private const string     Command    = "/gentle";
    public static IPluginLog PluginLog { get; private set; } = null!;

    // Search for `HidD_GetPreparsedData`, look for the function, which needs `Capabilities.OutputReportXY`
    private const string WriteFileHidDeviceReportSignature = "E8 ?? ?? ?? ?? 8B 4B 1C 8B F8"; // 7.0

    //NOTE (Chiv) RowId of ClassJob sheet
    private static readonly HashSet<uint> JobsWhitelist = new()
    {
        19, 20, 21, 22, 23, 24, 25, 27, 28, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42
    };

    //NOTE (Chiv) RowId of ClassJobCategory sheet
    private static readonly HashSet<uint> JobCategoryWhitelist = new()
    {
        20, 21, 22, 23, 24, 25, 26, 28, 29, 92, 96, 98, 99, 111, 112, 129, 149, 150, 180, 181, 196, 197
    };

    #region Hooks & Delegates

    [Signature(WriteFileHidDeviceReportSignature)]
    private readonly Delegates.WriteFileHidDOutputReport _writeFileHidDOutputReport = null!;


    // Search for 'SetState' and go up the chain
    [Signature("40 55 53 56 48 8B EC 48 83 EC 70 33 F6")]
    private readonly Delegates.FFXIVSetState _ffxivSetState = null!;

// #if DEBUG
//     [Signature("E8 ?? ?? ?? ?? 48 81 C4 ?? ?? ?? ?? 5E 5B 5D C3 49 8B 9A")]
//     private readonly XInputWrapperSetState _xInputWrapperSetState = null!;
// #endif

    // Alternative
    // 40 56 57 41 56 48 81 EC ?? ?? ?? ?? 44 0F 29 84 24 ?? ?? ?? ??
    // [Signature("40 ?? 57 41 ?? 48 81 EC ?? ?? ?? ?? 44 0F ?? ?? ?? ?? ?? ?? ?? 48 8B"
    //   , DetourName = nameof(DualsenseControllerPollDetour))]
    private readonly Hook<Delegates.ControllerPoll> _controllerPoll;

    [Signature(WriteFileHidDeviceReportSignature, DetourName = nameof(WriteFileHidDOutputReportDetour))]
    private readonly Hook<Delegates.WriteFileHidDOutputReport> _writeFileHidDOutputReportHook = null!;

    [Signature("48 83 EC 38 0F B6 81", DetourName = nameof(DeviceChangeDetour))]
    private readonly Hook<Delegates.DeviceChangeDelegate> _deviceChangeHook = null!;

    private Hook<Delegates.ParseRawInputReport>? _parseRawInputReportHook;

    [Signature("E8 ?? ?? ?? ?? EB 19 40 84 ED")]
    private static Delegates.DrawWeapon _drawWeapon = null!;

#if DEBUG
    //TODO
    [Signature("E8 ?? ?? ?? ?? EB 19 40 84 ED", DetourName = nameof(DrawWeaponDetour))]
    private readonly Hook<Delegates.DrawWeapon> _drawWeaponHook = null!;

    // TODO figure out how to correctly read it
    [Signature("48 8D 0D ?? ?? ?? ?? 80 E2", ScanType = ScanType.StaticAddress)]
    private nuint Unsheated;

    [Signature("48 8D 0D ?? ?? ?? ?? 80 E2", Offset = 3, ScanType = ScanType.StaticAddress)]
    private nuint UnsheatedOffSet3;

    [Signature("48 8D 0D ?? ?? ?? ?? 80 E2", Offset = 4, ScanType = ScanType.StaticAddress)]
    private nuint UnsheatedOffSet4;

    [Signature("48 8D 0D ?? ?? ?? ?? 80 E2", ScanType = ScanType.Text)]
    private nuint UnsheatedText;

    [Signature("48 8D 0D ?? ?? ?? ?? 80 E2", Offset = 3, ScanType = ScanType.Text)]
    private nuint UnsheatedOffSet3Text;

    [Signature("48 8D 0D ?? ?? ?? ?? 80 E2", Offset = 4, ScanType = ScanType.Text)]
    private nuint UnsheatedOffSet4Text;
#endif

    #endregion

    private readonly nint _parseRawDualShock4InputReportAddress;
    private readonly nint _parseRawDualSenseInputReportAddress;

    private readonly        IDalamudPluginInterface              _pluginInterface;
    private readonly        IClientState                        _clientState;
    private readonly        IObjectTable                        _objects;
    private readonly        IFramework                          _framework;
    private readonly        ICommandManager                     _commands;
    private readonly        ICondition                          _condition;
    private readonly        IGameInteropProvider                _gameInteropProvider;
    private readonly        IReadOnlyCollection<ClassJob>       _jobs;
    private readonly        IReadOnlyCollection<FFXIVAction>    _allActions;
    private readonly        PriorityQueue<CooldownTrigger, int> _queue = new();
    private readonly        Configuration                       _config;
    private readonly        AetherCurrentTrigger                _aetherCurrentTrigger;
    private static readonly byte[]                              SheatheBytes = "/sheathe motion\0"u8.ToArray();
    private static readonly byte[]                              DrawBytes    = "/draw motion\0"u8.ToArray();

    private IEnumerator<VibrationPattern.Step?>? _currentEnumerator;
    private VibrationTrigger?                    _highestPriorityTrigger;

    // Init in method called from constructor
    private Action<int, int> _setControllerState = null!;

    private nint _controllerStruct;
    private bool _shouldDrawConfigUi;
    private bool _isDisposed;
    private bool _noGamepadAttached;
    private bool _isDualSense;
    private int  _hidDevice = 1;
    private byte _dualSenseOutputReportFlag0;

    private byte _dualSenseOutputReportFlag2;

    // TODO: This is such a bandaid and needs proper state handling, if expanded
    private bool _createPressedLastReport;
    private bool _psHomePressedLastReport;

    // TODO: At the end of time, maybe this behemoth will be refactored into separate classes
    private SimulatedTriggerState _l2SimulatedTriggerState = SimulatedTriggerState.None;
    private SimulatedTriggerState _r2SimulatedTriggerState = SimulatedTriggerState.None;
    private TriggerState          _l2TriggerState          = TriggerState.None;
    private TriggerState          _r2TriggerState          = TriggerState.None;

#if DEBUG
        private int _rightMotorSpeed = 100;
        private int _leftMotorSpeed;
        private int _dwControllerIndex = 1;
        private int _cooldownGroup = CooldownTrigger.GCDCooldownGroup;
        private readonly int[] _lastReturnedFromPoll = new int[100];
        private int _currentIndex;
#endif

    public GentleTouch(
        IDalamudPluginInterface pi
      , ISigScanner            sigScanner
      , IClientState           clientState
      , IDataManager           data
      , IObjectTable           objects
      , ICommandManager        commands
      , IFramework             framework
      , ICondition             condition
      , IPluginLog             pluginLog
      , IGameInteropProvider   gameInteropProvider
    )
    {
        PluginLog = pluginLog;
        var config = pi.GetPluginConfig() as Configuration ?? new Configuration();
        _pluginInterface     = pi;
        _clientState         = clientState;
        _commands            = commands;
        _objects             = objects;
        _framework           = framework;
        _condition           = condition;
        _config              = config;
        _gameInteropProvider = gameInteropProvider;
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

        gameInteropProvider.InitializeFromAttributes(this);

        // TODO: Find all of them for 7.0
        var controllerPollAddress
            = sigScanner.ScanText("40 ?? 57 41 ?? 48 81 EC ?? ?? ?? ?? 44 0F ?? ?? ?? ?? ?? ?? ?? 48 8B");
        _parseRawDualSenseInputReportAddress  = sigScanner.ScanText("E8 ?? ?? ?? ?? 8B 4B 08 48 8D 55 A0");
        _parseRawDualShock4InputReportAddress = sigScanner.ScanText("E8 ?? ?? ?? ?? EB 2C 0F B7 53 22");

        CheckForGamepads();
        _controllerPoll = gameInteropProvider.HookFromAddress<Delegates.ControllerPoll>(
            controllerPollAddress,
            _isDualSense ? DualsenseControllerPollDetour : ControllerPollDetour);
        _controllerPoll.Enable();
        _writeFileHidDOutputReportHook.Enable();
        _deviceChangeHook.Enable();

        #endregion

        #region Excel Data

        //SAFETY: If this returns null, something bigger broke
        _jobs = data.Excel.GetSheet<ClassJob>()!
                    .Where(j => JobsWhitelist.Contains(j.RowId))
                    .ToArray();
        var actions = data.Excel.GetSheet<FFXIVAction>()!
                          .Where(a => a.IsPlayerAction && a.CooldownGroup != CooldownTrigger.GCDCooldownGroup &&
                                      !a.IsPvP);
        var gcdActions = data.Excel.GetSheet<FFXIVAction>()!
                             .Where(a =>
                                  a is { IsPlayerAction: true, IsPvP: false, CooldownGroup: CooldownTrigger.GCDCooldownGroup, IsRoleAction: false }
                                  && JobCategoryWhitelist.Contains(a.ClassJobCategory.Row))
                             .GroupBy(
                                  a => a.ClassJobCategory.Row,
                                  (_, group) => group.First()
                              );
        var allActions = actions.Concat(gcdActions);
        _allActions = allActions as FFXIVAction[] ?? allActions.ToArray();

        #endregion

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
            OnLogin();
        }
    }

    private void CheckForGamepads()
    {
        //                                                                   Sony         DualSense
        var connectedDeviceDefinitionsTask = new FilterDeviceDefinition(0x54C, 0xCE6, label: "DualSense")
                                            .CreateWindowsHidDeviceFactory()
                                            .GetConnectedDeviceDefinitionsAsync();
        // Blocking here should be okay, because the enumeration of devices should be done in a heartbeat,
        // but it needs to be asynchronous because of the nature of the underlying Windows API.
        connectedDeviceDefinitionsTask.Wait();
        var connectedDeviceDefinitions = connectedDeviceDefinitionsTask.Result;
        // We only support one single, via USB connected DualSense
        if (connectedDeviceDefinitions?.ToArray() is [{ ReadBufferSize: InputReport.SizeUsb }])
        {
            PluginLog.Debug("DualSense connected via USB.");
            _isDualSense = true;
        } else
        {
            // No DualSense, lets check for gamepad like devices in general
            const ushort gamePadUsageId = 0x05;
            connectedDeviceDefinitionsTask = new FilterDeviceDefinition(usagePage: 0x01, label: "Gamepads")
                                            .CreateWindowsHidDeviceFactory()
                                            .GetConnectedDeviceDefinitionsAsync();

            connectedDeviceDefinitionsTask.Wait();
            connectedDeviceDefinitions = connectedDeviceDefinitionsTask.Result;
            _noGamepadAttached         = connectedDeviceDefinitions != null && connectedDeviceDefinitions.All(x => x.Usage != gamePadUsageId);
        }

        PluginLog.Debug($"Is DualSense? {_isDualSense}");
        PluginLog.Debug($"No GamePad Attached? {_noGamepadAttached}");
        _setControllerState = _isDualSense ? DualSenseSetState : DefaultControllerSetState;

        _parseRawInputReportHook?.Disable();
        _parseRawInputReportHook?.Dispose();
        _parseRawInputReportHook = null;
        unsafe
        {
            _parseRawInputReportHook =
                _gameInteropProvider
                   .HookFromAddress<Delegates.ParseRawInputReport>(
                        _isDualSense ? _parseRawDualSenseInputReportAddress : _parseRawDualShock4InputReportAddress,
                        _isDualSense ? ParseDualSenseRawInputReportDetour : ParseDualShock4RawInputReportDetour
                    );
        }

        _parseRawInputReportHook.Enable();
    }

    private void OnLogout()
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

    private void OnLogin()
    {
        _pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        _pluginInterface.UiBuilder.Draw         += DrawUi;
        _framework.Update                       += FrameworkOutOfCombatUpdate;
        UpdateDualSenseState();
    }

    private void FrameworkInCombatUpdate(IFramework framework)
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
        var localPlayer = (_objects[0] as IPlayerCharacter)!;
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

    private void FrameworkInCombatPauseUpdate(IFramework framework)
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
        var localPlayer = (_objects[0] as IPlayerCharacter)!;
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
        var localPlayer  = (_objects[0] as IPlayerCharacter)!;
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

    private void FrameworkOutOfCombatUpdate(IFramework framework)
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

        if (_objects[0] is not IPlayerCharacter localPlayer)
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
            _ffxivSetState(_controllerStruct, rightMotorPercentage, leftMotorPercentage);
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
            report->Id                 = OutputReportUSB.IdUsb;
            report->reportCommon.Flag0 = (byte)(OutputReportFlag0.AdapterTriggerR2Select | OutputReportFlag0.AdapterTriggerL2Select);
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
                report->reportCommon.TriggerL2[0] = (byte)_config.DualSenseAdaptiveTriggerType;
                report->reportCommon.TriggerR2[0] = (byte)_config.DualSenseAdaptiveTriggerType;
                switch (_config.DualSenseAdaptiveTriggerType)
                {
                    case AdaptiveTriggerEffectType.Default:
                        break;
                    case AdaptiveTriggerEffectType.ContinuousResistance:
                    case AdaptiveTriggerEffectType.SectionResistance:
                        report->reportCommon.TriggerL2[1] = (byte)(_config.TriggerL2StartPosition / 100f * byte.MaxValue);
                        report->reportCommon.TriggerL2[2] = (byte)(_config.TriggerL2ForceOrEndPosition / 100f * byte.MaxValue);
                        report->reportCommon.TriggerR2[1] = (byte)(_config.TriggerR2StartPosition / 100f * byte.MaxValue);
                        report->reportCommon.TriggerR2[2] = (byte)(_config.TriggerR2ForceOrEndPosition / 100f * byte.MaxValue);
                        break;
                    case AdaptiveTriggerEffectType.Vibrate:
                        break;
                    case AdaptiveTriggerEffectType.Calibrate:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            } else
            {
                report->reportCommon.TriggerL2[0] = (byte)AdaptiveTriggerEffectType.Default;
                report->reportCommon.TriggerR2[0] = (byte)AdaptiveTriggerEffectType.Default;
            }

            _writeFileHidDOutputReport(_hidDevice, (nuint)report, OutputReportUSB.Size);
        }
    }

    private unsafe void UpdateTriggerState(
        GamepadInput*                 input
      , SimulatedTriggerState         state
      , Action<SimulatedTriggerState> updateState
      , TriggerState                  triggerState
      , ushort                        buttonMask
    )
    {
        switch (state)
        {
            case SimulatedTriggerState.None when triggerState is TriggerState.None:
                // input->ButtonsRaw = (ushort)(input->ButtonsRaw & ~mask);
                break;
            // TODO: This is basically never hit, because we always go from Light -> Full and hit single tap
            //  Would need to cache and check if full, before emitting light, but meh.
            // TODO: I think I would need to delay for X frames, to proper output a Full without a Light
            case SimulatedTriggerState.None when triggerState is TriggerState.Full:
                //input->ButtonsRaw = (ushort)(input->ButtonsRaw | mask);
                PluginLog.Info("None (Full) -> MimickDoubleTab");
                MimicDoubleTab();
                break;
            case SimulatedTriggerState.None when triggerState is TriggerState.Light:
                // PluginLog.Log("None (Light) -> SingleTap");
                updateState(SimulatedTriggerState.SingleTap);
                break;
            case SimulatedTriggerState.SingleTap when triggerState is TriggerState.None:
                // PluginLog.Log("SingleTap (None) -> None");
                updateState(SimulatedTriggerState.None);
                break;
            case SimulatedTriggerState.SingleTap when triggerState is TriggerState.Full:
                updateState(SimulatedTriggerState.DoubleTapNone);
                // PluginLog.Log("SingleTap (Full) -> DoubleTapNone -> MimickDoubleTab");
                _framework.RunOnTick(MimicDoubleTab, TimeSpan.FromMilliseconds(42));
                break;
            case SimulatedTriggerState.DoubleTapFirstTab:
                // PluginLog.Log("DoubleTabFirstTab");
                input->ButtonsRaw = (ushort)(input->ButtonsRaw | buttonMask);
                break;
            case SimulatedTriggerState.DoubleTapNone:
                // PluginLog.Log("DoubleTabNone");
                input->ButtonsRaw = (ushort)(input->ButtonsRaw & ~buttonMask);
                break;
            case SimulatedTriggerState.DoubleTabHold when triggerState is TriggerState.Full or TriggerState.Light:
                // PluginLog.Log("DoubleTabHold (Full/Light)");
                break;
            case SimulatedTriggerState.DoubleTabHold when triggerState is TriggerState.None:
                // PluginLog.Log("DoubleTabHold (None) -> None");
                updateState(SimulatedTriggerState.None);
                break;
            default:
                break;
        }

        return;

        void MimicDoubleTab()
        {
            // PluginLog.Log("??? -> DoubleTapFirstTab");
            updateState(SimulatedTriggerState.DoubleTapFirstTab);
            _framework.RunOnTick(() =>
            {
                // PluginLog.Log("DoubleTabFirstTab -> DoubleTapNone");
                updateState(SimulatedTriggerState.DoubleTapNone);
                _framework.RunOnTick(() =>
                {
                    // PluginLog.Log("DoubleTapNone -> DoubleTabHold");
                    updateState(SimulatedTriggerState.DoubleTabHold);
                }, TimeSpan.FromMilliseconds(35));
            }, TimeSpan.FromMilliseconds(35));
        }
    }

    #region Detour

    private nuint DeviceChangeDetour(nuint inputDeviceManager)
    {
        CheckForGamepads();
        return _deviceChangeHook.Original(inputDeviceManager);
    }

    private int ControllerPollDetour(nint gamepadInput)
    {
        _controllerStruct = gamepadInput;
        _controllerPoll.Disable();

        return _controllerPoll.Original(gamepadInput);
    }

    private int DualsenseControllerPollDetour(nint gamepadInput)
    {
        var result = _controllerPoll.Original(gamepadInput);
        if (!_config.DualSenseTriggerCrossHotBarActivation) return result;
        unsafe
        {
            var input = (GamepadInput*)gamepadInput;
            UpdateTriggerState(input,
                _l2SimulatedTriggerState,
                state => _l2SimulatedTriggerState = state,
                _l2TriggerState,
                (ushort)GamepadButtons.L2);
            UpdateTriggerState(input,
                _r2SimulatedTriggerState,
                state => _r2SimulatedTriggerState = state,
                _r2TriggerState,
                (ushort)GamepadButtons.R2);
        }

        return result;
    }

    private byte WriteFileHidDOutputReportDetour(int hidDevice, nuint outputReport, ushort reportLength)
    {
        _hidDevice = hidDevice;
        return _writeFileHidDOutputReportHook.Original(hidDevice, outputReport, reportLength);
    }

    private unsafe nuint ParseDualSenseRawInputReportDetour(nuint unk1, byte* rawReport, nuint unk3, byte unk4, nuint parseStructure)
    {
        var report   = (InputReport*)rawReport;
        var buttons1 = report->Buttons1;
        var buttons2 = report->Buttons2;
        _framework.RunOnFrameworkThread(() => PsExtraButtons(buttons1, buttons2));
        _l2TriggerState = ((report->Buttons1 & (byte)Buttons1.L2) > 0, report->L2) switch
        {
            (true, > 192)          => TriggerState.Full
          , (true, < 192 and > 42) => TriggerState.Light
          , _                      => TriggerState.None
        };
        _r2TriggerState = ((report->Buttons1 & (byte)Buttons1.R2) > 0, report->R2) switch
        {
            (true, > 192)          => TriggerState.Full
          , (true, < 192 and > 42) => TriggerState.Light
          , _                      => TriggerState.None
        };

        //SAFETY: The detour is only called if the hook is set.
        var result = _parseRawInputReportHook!.Original(unk1, rawReport, unk3, unk4, parseStructure);
        return result;
    }

    private unsafe nuint ParseDualShock4RawInputReportDetour(nuint unk1, byte* rawReport, nuint reportLength, byte unk4, nuint parseStructure)
    {
        var report   = (Interop.DualShock4.InputReport*)rawReport;
        var buttons1 = report->Buttons1;
        var buttons2 = report->Buttons2;
        // TODO: Does changing the button locals to class fields and queuing PSExtraButtons directly remove the lambda
        // class instance and reduces memory usage? Increases performance? Or decreases, because of worse cache?
        _framework.RunOnFrameworkThread(() => PsExtraButtons(buttons1, buttons2));

        //SAFETY: The detour is only called if the hook is set.
        var result = _parseRawInputReportHook!.Original(unk1, rawReport, reportLength, unk4, parseStructure);
        return result;
    }

    private unsafe void PsExtraButtons(byte buttons1, byte buttons2)
    {
        var createPressed = (buttons1 & (byte)Buttons1.Create) > 0;
        if (createPressed && !_createPressedLastReport)
        {
            var macro    = RaptureMacroModule.Instance()->GetMacro(0, 96);
            var instance = RaptureShellModule.Instance();
            if (macro != null && !instance->MacroLocked)
            {
                instance->ExecuteMacro(macro);
            }
        }

        _createPressedLastReport = createPressed;
        var psHomePressed = (buttons2 & (byte)Buttons2.PsHome) > 0;
        if (psHomePressed && !_psHomePressedLastReport)
        {
            var raptureShellModule = RaptureShellModule.Instance();
            if (_config.PsButtonDrawWeapon)
            {
                var isWeaponDrawn = &UIState.Instance()->WeaponState.IsUnsheathed;
                if (!raptureShellModule->MacroLocked && AgentMap.Instance()->IsPlayerMoving == 0)
                {
                    var drawWeaponMacro = stackalloc RaptureMacroModule.Macro[1];
                    drawWeaponMacro->Name.BufUsed             = 1;
                    drawWeaponMacro->Name.IsEmpty             = true;
                    drawWeaponMacro->Name.StringLength        = 0;
                    drawWeaponMacro->Name.StringPtr           = drawWeaponMacro->Name.StringPtr;
                    drawWeaponMacro->Name.StringPtr[0]        = 0;
                    drawWeaponMacro->Name.BufSize             = 0x40;
                    drawWeaponMacro->Name.IsUsingInlineBuffer = true;

                    foreach (var line in drawWeaponMacro->Lines.PointerEnumerator())
                    {
                        line->BufUsed             = 1;
                        line->IsEmpty             = true;
                        line->StringLength        = 0;
                        line->StringPtr           = line->InlineBuffer.GetPointer(0);
                        line->StringPtr[0]        = 0;
                        line->BufSize             = 0x40;
                        line->IsUsingInlineBuffer = true;
                    }

                    fixed (byte* cStr = *isWeaponDrawn ? SheatheBytes : DrawBytes)
                    {
                        drawWeaponMacro->Lines[0].SetString(cStr);
                        raptureShellModule->ExecuteMacro(drawWeaponMacro);
                    }

                } else
                {
                    _drawWeapon((nuint)isWeaponDrawn, !(*isWeaponDrawn));
                }
            } else
            {
                var macro = RaptureMacroModule.Instance()->GetMacro(0, 97);
                if (macro != null && !raptureShellModule->MacroLocked)
                {
                    raptureShellModule->ExecuteMacro(macro);
                }
            }
        }

        _psHomePressedLastReport = psHomePressed;
    }

#if DEBUG
    private unsafe byte DrawWeaponDetour(nuint unsheathed, bool isDrawn)
    {
        var result = _drawWeaponHook.Original(unsheathed, isDrawn);
        PluginLog.Log($"drawWeapon({unsheathed:x8}, {isDrawn}) -> {result}");
        PluginLog.Log($"{nameof(Unsheated)}:{Unsheated:x8}");
        PluginLog.Log($"{nameof(UnsheatedOffSet3)}:{UnsheatedOffSet3:x8}");
        PluginLog.Log($"{nameof(UnsheatedOffSet4)}:{UnsheatedOffSet4:x8}");
        PluginLog.Log($"{nameof(UnsheatedText)}:{UnsheatedText:x8}");
        PluginLog.Log($"{nameof(UnsheatedOffSet3Text)}:{UnsheatedOffSet3Text:x8}");
        PluginLog.Log($"{nameof(UnsheatedOffSet4)}:{UnsheatedOffSet4:x8}");
        var state = &UIState.Instance()->WeaponState.IsUnsheathed;
        PluginLog.Log($"WeaponState.Unsheathed:{(nuint)state:x8}");
        return result;
    }
#endif

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
            OnLogout();
            _clientState.Login  -= OnLogin;
            _clientState.Logout -= OnLogout;
            _commands.RemoveHandler(Command);
        }

        // NOTE (Chiv) Implicit, GC? call and explicit, non GC? call - remove unmanaged thingies.
        // Throws NPE if constructor/injection fails, so we safe-call here anyway.
        _controllerPoll?.Dispose();
        _writeFileHidDOutputReportHook?.Dispose();
        _deviceChangeHook?.Dispose();
        _parseRawInputReportHook?.Dispose();
#if DEBUG
        _drawWeaponHook.Dispose();
#endif
        _isDisposed = true;
    }

    ~GentleTouch()
    {
        Dispose(false);
    }

    #endregion
}