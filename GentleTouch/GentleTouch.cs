using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Actors;
using Dalamud.Game.ClientState.Actors.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Plugin;
using GentleTouch.Collection;
using Lumina.Excel.GeneratedSheets;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

// TODO 5 Refactor DrawCombo to generic
namespace GentleTouch
{
    public partial class GentleTouch : IDisposable
    {
        private const string Command = "/gentle";
        public const string PluginName = "GentleTouch";

        private static readonly HashSet<string> JobsWhitelist = new()
        {
            "warrior",
            "paladin",
            "dark knight",
            "gunbreaker",
            "white mage",
            "scholar",
            "astrologian",
            "ninja",
            "samurai",
            "dragoon",
            "monk",
            "bard",
            "dancer",
            "machinist",
            "black mage",
            "red mage",
            "blue mage",
            "summoner",
            "sage"
        };

        //TODO Other languages
        private readonly HashSet<string> _aetherCurrentNameWhitelist = new()
        {
            "Aether Current",
            "Windätherquelle",
            "vent éthéré",
            "風脈の泉"
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
        private readonly IReadOnlyCollection<ClassJob> _jobs;
        private readonly IReadOnlyCollection<FFXIVAction> _allActions;
        private readonly PriorityQueue<VibrationCooldownTrigger, int> _queue = new();
        private IEnumerator<VibrationPattern.Step?>? _currentEnumerator;
        private VibrationTrigger? _highestPriorityTrigger;
        private readonly Configuration _config;

#if DEBUG
        private int _rightMotorSpeed = 100;
        private int _leftMotorSpeed;
        private int _dwControllerIndex = 1;
        private int _cooldownGroup = VibrationCooldownTrigger.GCDCooldownGroup;
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


        public GentleTouch(DalamudPluginInterface pi, Configuration config)
        {
            #region Signatures

            // NOTE (Chiv): Different signatures from different methods
            const string maybeControllerPollSignature =
                "40 ?? 57 41 ?? 48 81 EC ?? ?? ?? ?? 44 0F ?? ?? ?? ?? ?? ?? ?? 48 8B";
            // TODO (chiv): '??'s at signatures end can be omitted, can't they?
            //const string maybeControllerPollSignatureAlternative =
            //    "40 56 57 41 56 48 81 EC ?? ?? ?? ?? 44 0F 29 84 24 ?? ?? ?? ??";
            const string xInputWrapperSetStateSignature =
                "48 ff 25 69 28 ce 01 cc cc cc cc cc cc cc cc cc 48 89 5c 24";
            //const string xInputWrapperSetStateSignatureAlternative =
            //    "48 FF ?? ?? ?? ?? ?? CC CC CC CC CC CC CC CC CC 48 89 ?? ?? ?? 48 89 ?? ?? ?? 48 89 ?? ?? ?? 48 89";
            const string ffxivSetStateSignature =
                "40 55 53 56 48 8b ec 48 81 ec 80 00 00 00 33 f6 44 8b d2 4c 8b c9";
            //const string ffxivSetStateSignatureAlternative =
            //    "40 ?? 53 56 48 8B ?? 48 81 EC";
            //NOTE (Chiv): Signature from :
            // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/Helper/Common.cs
            const string actionManagerSignature = "E8 ?? ?? ?? ?? 33 C0 E9 ?? ?? ?? ?? 8B 7D 0C";
            //NOTE (Chiv): Signature from :
            // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/GameStructs/ActionManager.cs
            const string getActionCooldownSlotSignature = "E8 ?? ?? ?? ?? 0F 57 FF 48 85 C0";

            #endregion


            _pluginInterface = pi;
            _config = config;
            // NOTE (Chiv) Resolve pattern GUIDs to pattern
            // TODO (Chiv) Better in custom Serializer?
            foreach (var trigger in _config.CooldownTriggers)
            {
                //TODO (Chiv) Error handling if something messed up guids.
                trigger.Pattern = _config.Patterns.FirstOrDefault(p => p.Guid == trigger.PatternGuid) ??
                                  new VibrationPattern();
            }

            _pluginInterface.ClientState.OnLogin += OnLogin;
            _pluginInterface.ClientState.OnLogout += OnLogout;
#if DEBUG
            if (_pluginInterface.ClientState.LocalPlayer is not null)
            {
                OnLogin(null!, null!);
            }
#endif

            #region Hooks, Functions and Addresses

            _ffxivSetState = Marshal.GetDelegateForFunctionPointer<FFXIVSetState>(
                _pluginInterface.TargetModuleScanner.ScanText(ffxivSetStateSignature));
            _xInputWrapperSetState = Marshal.GetDelegateForFunctionPointer<XInputWrapperSetState>(
                _pluginInterface.TargetModuleScanner.ScanText(xInputWrapperSetStateSignature));
            _controllerPoll = new Hook<MaybeControllerPoll>(
                _pluginInterface.TargetModuleScanner.ScanText(maybeControllerPollSignature),
                (MaybeControllerPoll) ControllerPollDetour);
            _controllerPoll.Enable();
            _actionManager =
                _pluginInterface.TargetModuleScanner.GetStaticAddressFromSig(actionManagerSignature);
            _getActionCooldownSlot = Marshal.GetDelegateForFunctionPointer<GetActionCooldownSlot>(
                _pluginInterface.TargetModuleScanner.ScanText(getActionCooldownSlotSignature));

            #endregion

            #region Excel Data

            _jobs = _pluginInterface.Data.Excel.GetSheet<ClassJob>()
                .Where(j => JobsWhitelist.Contains(j.Name))
                .ToArray();
            var actions = _pluginInterface.Data.Excel.GetSheet<FFXIVAction>()
                .Where(a => a.IsPlayerAction && a.CooldownGroup != VibrationCooldownTrigger.GCDCooldownGroup &&
                            !a.IsPvP);
            var gcdActions = _pluginInterface.Data.Excel.GetSheet<FFXIVAction>()
                // NOTE the ClassJobCategory.Name.Length is a hack,
                // as every Job (not class!) has up to two GCDs:
                // One for its Class, if available (ClassJobCategory.Name.Length==6(+1, whitespace), e.g 'LNC DRG')
                // and one for the Job itself (ClassJobCategory.Name.Length==3, e.g. 'DRG')
                // We do not want duplicates, so we just take the GCD for the Job and discard the Class ones.
                .Where(a =>
                    a.IsPlayerAction && !a.IsPvP && a.CooldownGroup == VibrationCooldownTrigger.GCDCooldownGroup
                    && !a.IsRoleAction && a.ClassJobCategory.Value.Name.ToString().Length == 3)
                .GroupBy(
                    a => a.ClassJobCategory.Row,
                    (_, group) => group.First()
                );
            var allActions = actions.Concat(gcdActions);
            _allActions = allActions as FFXIVAction[] ?? allActions.ToArray();

            #region BreakingConfigurationVersionMigration

            //TODO Remove before v1.0
            if (config.Version == 0)
            {
                // Risk Bool Migration
                if (config.RisksAcknowledged)
                {
                    config.OnboardingStep = Onboarding.Done;
                }

                // GCD Migration
                var gcdTrigger = _config.CooldownTriggers.FirstOrDefault(t => t.JobId == 0);
                if (gcdTrigger is not null)
                {
                    _config.CooldownTriggers.Remove(gcdTrigger);
                    for (var i = 0; i < config.CooldownTriggers.Count; i++)
                        config.CooldownTriggers[i].Priority = i;
                    foreach (var job in _jobs)
                    {
                        var action = _allActions
                            .Where(a => a.CooldownGroup == VibrationCooldownTrigger.GCDCooldownGroup)
                            .First(a => a.ClassJobCategory.Value.HasClass(job.RowId));
                        var lastTrigger = _config.CooldownTriggers.LastOrDefault();
                        _config.CooldownTriggers.Add(
                            new VibrationCooldownTrigger(
                                job.RowId,
                                action.Name,
                                action.RowId,
                                action.CooldownGroup,
                                lastTrigger?.Priority + 1 ?? 0,
                                gcdTrigger.Pattern
                            ));
                    }
                }

                config.Version = 1;
                _pluginInterface.SavePluginConfig(_config);
            }

            #endregion

            #endregion

            pi.CommandManager.AddHandler(Command, new CommandInfo((_, _) => { OnOpenConfigUi(null!, null!); })
            {
                HelpMessage = "Open GentleTouch configuration menu.",
                ShowInHelp = true
            });
        }

        private void OnLogout(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi -= OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi -= BuildUi;
            _pluginInterface.Framework.OnUpdateEvent -= FrameworkOutOfCombatUpdate;
            _pluginInterface.Framework.OnUpdateEvent -= FrameworkInCombatUpdate;
            _currentEnumerator?.Dispose();
            _currentEnumerator = null;
            _highestPriorityTrigger = null;
            _queue.Clear();
        }

        private void OnLogin(object sender, EventArgs e)
        {
            _pluginInterface.UiBuilder.OnOpenConfigUi += OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi += BuildUi;
            _pluginInterface.Framework.OnUpdateEvent += FrameworkOutOfCombatUpdate;
            _currentEnumerator = GetAetherCurrentSenseEnumerator();
        }

        private IEnumerator<VibrationPattern.Step?> GetAetherCurrentSenseEnumerator()
        {
            var step = new VibrationPattern.Step(15, 15);
            var zeroStep = new VibrationPattern.Step(0, 0);
            var nextTimeStep = 0L;
            while (true)
            {
                while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                    yield return null;
                if (!_config.SenseAetherCurrents)
                {
                    yield return null;
                    continue;
                }

                var localPlayer = _pluginInterface.ClientState.LocalPlayer;
                if (localPlayer is null)
                {
                    yield return null;
                    continue;
                }
                
                var distance = (
                    from Actor a in _pluginInterface.ClientState.Actors
                    where a is not null
                        && a.ObjectKind == ObjectKind.EventObj
                        && _aetherCurrentNameWhitelist.Contains(a.Name)
                        && Marshal.ReadByte(a.Address, 0x105) != 0
                    select (float?) Math.Sqrt(Math.Pow(localPlayer.Position.X - a.Position.X, 2)
                                              + Math.Pow(localPlayer.Position.Y - a.Position.Y, 2)
                                              + Math.Pow(localPlayer.Position.Z - a.Position.Z, 2))
                ).Min() ?? float.MaxValue;
                if (distance > _config.MaxAetherCurrentSenseDistance)
                {
                    yield return null;
                    continue;
                }
                nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 200;
                yield return step;
                while (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < nextTimeStep)
                    yield return null;
                // Silence after the vibration depending on distance to Aether Current
                var msTillNextStep = Math.Max((long) (800L * (distance / _config.MaxAetherCurrentSenseDistance)),
                    10L);
                nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + msTillNextStep;
                yield return zeroStep;
            }
        }

        private void FrameworkInCombatUpdate(Framework framework)
        {
            void InitiateOutOfCombatLoop()
            {
                ControllerSetState(0, 0);
                _currentEnumerator = GetAetherCurrentSenseEnumerator();
                _pluginInterface.Framework.OnUpdateEvent += FrameworkOutOfCombatUpdate;
                _pluginInterface.Framework.OnUpdateEvent -= FrameworkInCombatUpdate;
            }

            if ((!_pluginInterface.ClientState.LocalPlayer?.IsStatus(StatusFlags.InCombat) ?? true)
                || _pluginInterface.ClientState.Condition[ConditionFlag.Unconscious])
            {
                _queue.Clear();
                _highestPriorityTrigger = null;
                _currentEnumerator?.Dispose();
                _currentEnumerator = null;
                foreach (var ct in _config.CooldownTriggers)
                {
                    ct.ShouldBeTriggered = false;
                }

                InitiateOutOfCombatLoop();
                return;
            }

            var weaponSheathed = _config.NoVibrationWithSheathedWeapon &&
                                 !_pluginInterface.ClientState.LocalPlayer!.IsStatus(StatusFlags.WeaponOut);
            if (weaponSheathed)
            {
                InitiateOutOfCombatLoop();
                return;
            }

            var casting = _config.NoVibrationDuringCasting &&
                          _pluginInterface.ClientState.LocalPlayer!.IsStatus(StatusFlags.Casting);
            if (casting)
            {
                InitiateOutOfCombatLoop();
                return;
            }

            EnqueueCooldownTriggers();
            UpdateHighestPriorityTrigger();
            CheckAndVibrate();
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
            //TODO (Chiv) Proper Switch for trigger types
            var cooldowns =
                _config.CooldownTriggers
                    .Where(t => t.JobId == _pluginInterface.ClientState.LocalPlayer.ClassJob.Id)
                    .Select(t => (t, _getActionCooldownSlot(_actionManager, t.ActionCooldownGroup - 1)));

            var tuples = cooldowns as (VibrationCooldownTrigger t, Cooldown c)[] ?? cooldowns.ToArray();
            // Check for all triggers _in_ cooldown state and set ShouldBeTriggered to true
            // -> We want them to be triggered when leaving the cooldown state!
            foreach (var (t, _) in tuples.Where(it => it.c))
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
            var triggeredTriggers = tuples.Where(it => !it.c && it.t.ShouldBeTriggered).Select(it => it.t);
            var triggers = triggeredTriggers as VibrationCooldownTrigger[] ?? triggeredTriggers.ToArray();
            foreach (var trigger in triggers) trigger.ShouldBeTriggered = false;
            _queue.EnqueueRange(triggers.Select(t => (t, t.Priority)));
        }

        private void FrameworkOutOfCombatUpdate(Framework framework)
        {
            var inCombat = _pluginInterface.ClientState.LocalPlayer?.IsStatus(StatusFlags.InCombat) ?? false;
            if (!inCombat)
            {
                CheckAndVibrate();
                return;
            }

            ;
            var weaponSheathed = _config.NoVibrationWithSheathedWeapon &&
                                 !_pluginInterface.ClientState.LocalPlayer!.IsStatus(StatusFlags.WeaponOut);
            if (weaponSheathed) return;
            var casting = _config.NoVibrationDuringCasting &&
                          _pluginInterface.ClientState.LocalPlayer!.IsStatus(StatusFlags.Casting);
            if (casting) return;
            _currentEnumerator = null;
            ControllerSetState(0, 0);
            _pluginInterface.Framework.OnUpdateEvent += FrameworkInCombatUpdate;
            _pluginInterface.Framework.OnUpdateEvent -= FrameworkOutOfCombatUpdate;
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
#if !DEBUG
            var original = _controllerPoll.Original(maybeControllerStruct);
            _lastReturnedFromPoll[_currentIndex++ % _lastReturnedFromPoll.Length] = original;
            // TODO (Chiv) Interpretation happens inside method, log appears after map (0x40 = Square/X)
            //if(original is 0x40) PluginLog.Warning("Should block!");
            //return original is 0x40 ? 0 : original;
            return original;
#else
            _controllerPoll.Disable();
            return _controllerPoll.Original(maybeControllerStruct);
#endif
        }

        #region UI

        private void BuildUi()
        {
            _shouldDrawConfigUi = _shouldDrawConfigUi &&
                                  ConfigurationUi.DrawConfigUi(_config, _pluginInterface,
                                      _pluginInterface.SavePluginConfig, _jobs, _allActions, ref _currentEnumerator);

#if DEBUG
            DrawDebugUi();
#endif
        }

        private void OnOpenConfigUi(object sender, EventArgs e)
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
                _pluginInterface.ClientState.OnLogin -= OnLogin;
                _pluginInterface.ClientState.OnLogout -= OnLogout;
                _pluginInterface.CommandManager.RemoveHandler(Command);
            }

            // NOTE (Chiv) Implicit, GC? call and explicit, non GC? call - remove unmanaged thingies.
            if (_controllerPoll.IsEnabled) _controllerPoll.Disable();
            _controllerPoll.Dispose();

            _isDisposed = true;
        }

        ~GentleTouch()
        {
            Dispose(false);
        }

        #endregion
    }
}