using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Plugin;
using GentleTouch.Collection;
using GentleTouch.Interop;
using GentleTouch.Triggers;
using Lumina.Excel.GeneratedSheets;
using Condition = Dalamud.Game.ClientState.Conditions.Condition;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace GentleTouch
{
    public partial class GentleTouch : IDalamudPlugin
    {
        private const string Command = "/gentle";
        public const string PluginName = "GentleTouch";
        public string Name => PluginName;

        //NOTE (Chiv) RowId of ClassJob sheet
        private static readonly HashSet<uint> JobsWhitelist = new()
        {
            19,20,21,22,23,24,25,27,28,30,31,32,33,34,35,36,37,38,39,40
        };

        //NOTE (Chiv) RowId of ClassJobCategory sheet
        private static readonly HashSet<uint> JobCategoryWhitelist = new()
        {
            20,21,22,23,24,25,26,28,29,92,96,98,99,111,112,129,149,150,180,181
        };

        // TODO (Chiv): Check Right and Left Motor for x360/XOne Gamepad
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void FFXIVSetState(nint maybeControllerStruct, int rightMotorSpeedPercent,
            int leftMotorSpeedPercent);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int XInputWrapperSetState(int dwUserIndex, ref XInputVibration pVibration);

        private delegate int MaybeControllerPoll(nint maybeControllerStruct);

        // NOTE (Chiv) modified from
        // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/GameStructs/ActionManager.cs
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate ref Cooldown GetActionCooldownSlot(nint actionManager, int cooldownGroup);

        private readonly FFXIVSetState _ffxivSetState;
        private readonly XInputWrapperSetState _xInputWrapperSetState;
        private readonly Hook<MaybeControllerPoll> _controllerPoll;
        private readonly nint _actionManager;
        private readonly GetActionCooldownSlot _getActionCooldownSlot;
        private readonly DalamudPluginInterface _pluginInterface;
        private readonly ClientState _clientState;
        private readonly ObjectTable _objects;
        private readonly Framework _framework;
        private readonly CommandManager _commands;
        private readonly Condition _condition;
        private readonly IReadOnlyCollection<ClassJob> _jobs;
        private readonly IReadOnlyCollection<FFXIVAction> _allActions;
        private readonly PriorityQueue<CooldownTrigger, int> _queue = new();
        private readonly Configuration _config;
        private readonly AetherCurrentTrigger _aetherCurrentTrigger;
        private IEnumerator<VibrationPattern.Step?>? _currentEnumerator;
        private VibrationTrigger? _highestPriorityTrigger;

#if DEBUG
        private int _rightMotorSpeed = 100;
        private int _leftMotorSpeed;
        private int _dwControllerIndex = 1;
        private int _cooldownGroup = CooldownTrigger.GCDCooldownGroup;
        private readonly int[] _lastReturnedFromPoll = new int[100];
        private int _currentIndex;
#endif

        private nint _maybeControllerStruct;

        private bool _shouldDrawConfigUi =
#if DEBUG
                true
#else
            false
#endif
            ;

        private bool _isDisposed;


        /*
         * DalamudPluginInterface pluginInterface,
        BuddyList buddies,
        ChatGui chat,
        ChatHandlers chatHandlers,
        ClientState clientState,
        CommandManager commands,
        Condition condition,
        DataManager data,
        FateTable fates,
        FlyTextGui flyText,
        Framework framework,
        GameGui gameGui,
        GameNetwork gameNetwork,
        JobGauges gauges,
        KeyState keyState,
        LibcFunction libcFunction,
        ObjectTable objects,
        PartyFinderGui pfGui,
        PartyList party,
        SeStringManager seStringManager,
        SigScanner sigScanner,
        TargetManager targets,
        ToastGui toasts
         */
        
        public GentleTouch(DalamudPluginInterface pi
            , SigScanner sigScanner
            , ClientState clientState
            , DataManager data
            , ObjectTable objects
            , CommandManager commands
            , Framework framework
            , Condition condition)
        {
            #region Signatures
            // NOTE (Chiv): Different signatures from different methods
            const string maybeControllerPollSignature =
                "40 ?? 57 41 ?? 48 81 EC ?? ?? ?? ?? 44 0F ?? ?? ?? ?? ?? ?? ?? 48 8B";
            //const string maybeControllerPollSignatureAlternative =
            //    "40 56 57 41 56 48 81 EC ?? ?? ?? ?? 44 0F 29 84 24 ?? ?? ?? ??";
            const string xInputWrapperSetStateSignature =
                "E8 ?? ?? ?? ?? 48 81 C4 ?? ?? ?? ?? 5E 5B 5D C3 49 8B 9A";
            //const string xInputWrapperSetStateSignatureAlternative =
            //    "48 FF ?? ?? ?? ?? ?? CC CC CC CC CC CC CC CC CC 48 89 ?? ?? ?? 48 89 ?? ?? ?? 48 89 ?? ?? ?? 48 89";
            const string ffxivSetStateSignature =
                "40 55 53 56 48 8b ec 48 81 ec 80 00 00 00 33 f6 44 8b d2 4c 8b c9";
            //const string ffxivSetStateSignatureAlternative =
            //    "40 ?? 53 56 48 8B ?? 48 81 EC"; //40 56 57 41 56 48 81 EC ? ? ? ? 44 0F 29 84 24 ? ? ? ?  
            //NOTE (Chiv): Signature from :
            // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/Helper/Common.cs
            const string actionManagerSignature = "E8 ?? ?? ?? ?? 33 C0 E9 ?? ?? ?? ?? 8B 7D 0C";
            //NOTE (Chiv): Signature from :
            // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/GameStructs/ActionManager.cs
            const string getActionCooldownSlotSignature = "E8 ?? ?? ?? ?? 0F 57 FF 48 85 C0";

            #endregion
            
            var config = pi.GetPluginConfig() as Configuration ?? new Configuration();
            _pluginInterface = pi;
            _clientState = clientState;
            _commands = commands;
            _objects = objects;
            _framework = framework;
            _condition = condition;
            _config = config;
            // NOTE (Chiv) Resolve pattern GUIDs to pattern
            // TODO (Chiv) Better in custom Serializer?
            foreach (var trigger in _config.CooldownTriggers)
            {
                trigger.Pattern = _config.Patterns.FirstOrDefault(p => p.Guid == trigger.PatternGuid) ??
                                  new VibrationPattern();
            }
            
            _clientState.Login += OnLogin;
            _clientState.Logout += OnLogout;

            #region Hooks, Functions and Addresses

            
            _ffxivSetState = Marshal.GetDelegateForFunctionPointer<FFXIVSetState>(
                sigScanner.ScanText(ffxivSetStateSignature));
            _xInputWrapperSetState = Marshal.GetDelegateForFunctionPointer<XInputWrapperSetState>(
                sigScanner.ScanText(xInputWrapperSetStateSignature));
            _controllerPoll = new Hook<MaybeControllerPoll>(
                sigScanner.ScanText(maybeControllerPollSignature),
                ControllerPollDetour);
            _controllerPoll.Enable();
            _actionManager =
                sigScanner.GetStaticAddressFromSig(actionManagerSignature);
            _getActionCooldownSlot = Marshal.GetDelegateForFunctionPointer<GetActionCooldownSlot>(
                sigScanner.ScanText(getActionCooldownSlotSignature));

            #endregion

            #region Excel Data
            
            //TODO Handle potential nulls
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

            _aetherCurrentTrigger = AetherCurrentTrigger.CreateAetherCurrentTrigger(
                () => _config.MaxAetherCurrentSenseDistanceSquared, _objects);
            _commands.AddHandler(Command, new CommandInfo((_, _) => { OnOpenConfigUi(); })
            {
                HelpMessage = "Open GentleTouch configuration menu.",
                ShowInHelp = true
            });

            // NOTE (Chiv) LocalPlayer != null => logged in, but plugin is just loading => do login logic
            if(_clientState.LocalPlayer is not null)
            {
                OnLogin(null!, null!);
            }

        }

        private void OnLogout(object? sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            _pluginInterface.UiBuilder.Draw -= DrawUi;
            _framework.Update -= FrameworkOutOfCombatUpdate;
            _framework.Update -= FrameworkInCombatUpdate;
            ControllerSetState(0,0);
            _currentEnumerator?.Dispose();
            _currentEnumerator = null;
            _highestPriorityTrigger = null;
            _queue.Clear();
        }

        private void OnLogin(object? sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            _pluginInterface.UiBuilder.Draw += DrawUi;
            _framework.Update += FrameworkOutOfCombatUpdate;
        }
        
        private void FrameworkInCombatUpdate(Framework framework)
        {
            if (!_condition[ConditionFlag.InCombat]
                || _condition[ConditionFlag.Unconscious] 
                || _condition[ConditionFlag.WatchingCutscene]
                || _condition[ConditionFlag.WatchingCutscene78]
                || _condition[ConditionFlag.OccupiedInCutSceneEvent])
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
                ControllerSetState(0, 0);
                _framework.Update += FrameworkInCombatPauseUpdate;
                _framework.Update -= FrameworkInCombatUpdate;
                return;
            }

            var casting = _config.NoVibrationDuringCasting &&
                          localPlayer.IsStatus(StatusFlags. IsCasting);
            if (casting)
            {
                ControllerSetState(0, 0);
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
                || _condition[ConditionFlag.OccupiedInCutSceneEvent])
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

            ControllerSetState(0, 0);
        }

        private void CheckAndVibrate()
        {
            if (_currentEnumerator is null) return;
            if (_currentEnumerator.MoveNext())
            {
                var s = _currentEnumerator.Current;
                if (s is not null)
                {
                    ControllerSetState(s.LeftMotorPercentage, s.RightMotorPercentage);
                }
            }
            else
            {
                ControllerSetState(0, 0);
                _currentEnumerator.Dispose();
                _currentEnumerator = null;
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
            _currentEnumerator = _highestPriorityTrigger.GetEnumerator();
            ControllerSetState(0, 0);
        }

        private void EnqueueCooldownTriggers()
        {
            
            var localPlayer = (_objects[0] as PlayerCharacter);
            var currentJobId = localPlayer?.ClassJob.Id ?? 0;

            foreach (var t in _config.CooldownTriggers)
            {
                if (t.JobId != currentJobId) continue;
                var c = _getActionCooldownSlot(_actionManager, t.ActionCooldownGroup - 1);
                // Check for all triggers _in_ cooldown state and set ShouldBeTriggered to true
                // -> We want them to be triggered when leaving the cooldown state!
                if (c)
                {
                    if (_highestPriorityTrigger == t)
                    {
                        // NOTE: Memory leak if not disposed, should exist if _highestPriorityTrigger is set
                        _currentEnumerator!.Dispose();
                        _currentEnumerator = null;
                        _highestPriorityTrigger = null;
                        ControllerSetState(0, 0);
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
                || _condition[ConditionFlag.OccupiedInCutSceneEvent])
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
                        ControllerSetState(0,0);
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
            ControllerSetState(0, 0);
            _framework.Update += FrameworkInCombatUpdate;
            _framework.Update -= FrameworkOutOfCombatUpdate;
        }

        private void ControllerSetState(int leftMotorPercentage, int rightMotorPercentage, bool direct = false,
            int dwControllerIndex = 0)
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

        private int ControllerPollDetour(nint maybeControllerStruct)
        {
            _maybeControllerStruct = maybeControllerStruct;
            _controllerPoll.Disable();
            return _controllerPoll.Original(maybeControllerStruct);
        }

        #region UI

        private void DrawUi()
        {
            // TODO (Chiv) Refactor to add/remove UI Event instead of boolean
            _shouldDrawConfigUi = _shouldDrawConfigUi &&
                                  ConfigurationUi.DrawConfigUi(_config, _pluginInterface,
                                      _pluginInterface.SavePluginConfig, _jobs, _allActions, ref _currentEnumerator);
#if DEBUG
            DrawDebugUi();
#endif
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
                _clientState.Login -= OnLogin;
                _clientState.Logout -= OnLogout;
                _commands.RemoveHandler(Command);
            }

            // NOTE (Chiv) Implicit, GC? call and explicit, non GC? call - remove unmanaged thingies.
            if (_controllerPoll?.IsEnabled ?? false) _controllerPoll.Disable();
            _controllerPoll?.Dispose();

            _isDisposed = true;
        }

        ~GentleTouch()
        {
            Dispose(false);
        }

        #endregion
    }
}