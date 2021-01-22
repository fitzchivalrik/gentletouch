using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using GentleTouch.Caraxi;
using GentleTouch.Collection;
using ImGuiNET;
using static System.Int32;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace GentleTouch
{
    public class GentleTouch : IDisposable
    {
        private const string Command = "/gentle";

        // TODO (Chiv): Check Right and Left Motor for x360/XOne Gamepad
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void FFXIVSetState(nint maybeControllerStruct, int rightMotorSpeedPercent, int leftMotorSpeedPercent);
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
        private Configuration _config;

        // TODO TESTING

        private int _rightMotorSpeed = 0;
        private int _leftMotorSpeed = 0;
        private int _cooldownGroup = 58;
        private int _nextStep = 0;
        private long _nextTimeStep = 0;
        private int[] _lastReturnedFromPoll = new int[100];
        private int _currentIndex = 0;

        private bool reset = false;
        private VibrationPattern _pattern = new()
        {
            Steps = new[]
            {
                new VibrationPattern.Step(50, 50, 200),
                new VibrationPattern.Step(0, 0, 200),
            },
            //Cycles = int.MaxValue,
            Infinite = true,
            Name = "SimplePattern"
        };

        private readonly List<VibrationCooldownTrigger> _debugTriggers = new();
        private static readonly IComparer<int> Comparer = Comparer<int>.Create((lhs, rhs) => rhs - lhs);
        private readonly PriorityQueue<VibrationCooldownTrigger, int> _queue= new (Comparer);

        private readonly SortedDictionary<int, VibrationCooldownTrigger> _dicQueue = new(Comparer);

        private IEnumerator<VibrationPattern.Step?>? _currentEnumerator;

        private VibrationCooldownTrigger? _currentTrigger;
        //TODO END TESTING


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
            const string maybeControllerPollSignature =
                "40 ?? 57 41 ?? 48 81 EC ?? ?? ?? ?? 44 0F ?? ?? ?? ?? ?? ?? ?? 48 8B";
            // TODO (chiv): '??'s at signatures end can be omitted, can't they?
            const string maybeControllerPollSignatureAlternative =
                "40 56 57 41 56 48 81 EC ?? ?? ?? ?? 44 0F 29 84 24 ?? ?? ?? ??";
            const string xInputWrapperSetStateSignature =
                "48 ff 25 69 28 ce 01 cc cc cc cc cc cc cc cc cc 48 89 5c 24";
            const string xInputWrapperSetStateSignatureAlternative = 
                "48 FF ?? ?? ?? ?? ?? CC CC CC CC CC CC CC CC CC 48 89 ?? ?? ?? 48 89 ?? ?? ?? 48 89 ?? ?? ?? 48 89";
            const string ffxivSetStateSignature =
                "40 55 53 56 48 8b ec 48 81 ec 80 00 00 00 33 f6 44 8b d2 4c 8b c9";
            const string ffxivSetStateSignatureAlternative =
                "40 ?? 53 56 48 8B ?? 48 81 EC";
            //NOTE (Chiv): Signature from :
            // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/Helper/Common.cs
            const string actionManagerSignature = "E8 ?? ?? ?? ?? 33 C0 E9 ?? ?? ?? ?? 8B 7D 0C";
            //NOTE (Chiv): Signature from :
            // https://github.com/Caraxi/SimpleTweaksPlugin/blob/078c48947fce3578d631cd2de50245005aba8fdd/GameStructs/ActionManager.cs
            const string getActionCooldownSlotSignature = "E8 ?? ?? ?? ?? 0F 57 FF 48 85 C0";
            #endregion
            
            // TODO TESTING
            //config.Patterns.RemoveAll(p => true);
            if (config.Patterns.Count == 0)
            {
             
                config.Patterns.Add(_pattern);
                pi.SavePluginConfig(config);
            }

            _debugTriggers.Add(new VibrationCooldownTrigger("GCD", -1, 58, MinValue, config.Patterns[1]));
            _debugTriggers.Add(new VibrationCooldownTrigger("Hide", 2245, 6, 1, config.Patterns[0]));
            _debugTriggers.Add(new VibrationCooldownTrigger("Shade Shift", 2241, 21, 2, config.Patterns[0]));
            // TODO END TESTING
            
            _pluginInterface = pi;
            _config = config;
            _pluginInterface.UiBuilder.OnOpenConfigUi += OnOpenConfigUi;
            _pluginInterface.UiBuilder.OnBuildUi += BuildUi;
            _pluginInterface.Framework.OnUpdateEvent += FrameworkOutOfCombatUpdate;

            #region Hooks, Functions and Addresses

            _ffxivSetState = Marshal.GetDelegateForFunctionPointer<FFXIVSetState>(
                _pluginInterface.TargetModuleScanner.ScanText(ffxivSetStateSignature));
            _xInputWrapperSetState = Marshal.GetDelegateForFunctionPointer<XInputWrapperSetState>(
                _pluginInterface.TargetModuleScanner.ScanText(xInputWrapperSetStateSignature));
            _controllerPoll = new Hook<MaybeControllerPoll>(
                _pluginInterface.TargetModuleScanner.ScanText(maybeControllerPollSignature),
                (MaybeControllerPoll) ControllerPollDetour );
            _controllerPoll.Enable();
            _actionManager =
                _pluginInterface.TargetModuleScanner.GetStaticAddressFromSig(actionManagerSignature);
            _getActionCooldownSlot = Marshal.GetDelegateForFunctionPointer<GetActionCooldownSlot>(
                _pluginInterface.TargetModuleScanner.ScanText(getActionCooldownSlotSignature));
            #endregion
            
            //TODO TESTING
            pi.CommandManager.AddHandler(Command, new CommandInfo((_, _) => { OnOpenConfigUi(null!, null!); })
            {
                HelpMessage = "Become gentle.",
                ShowInHelp = true
            });
            //TODO END TESTING
        }

        private void FrameworkInCombatUpdate(Framework framework)
        {
            var inCombat = this._pluginInterface.ClientState.LocalPlayer.IsStatus(StatusFlags.InCombat);
            if (!inCombat)
            {
                _queue.Clear();
                _currentTrigger = null;
                _currentEnumerator?.Dispose();
                _currentEnumerator = null;
                _ffxivSetState(_maybeControllerStruct, 0, 0);
                _pluginInterface.Framework.OnUpdateEvent += FrameworkOutOfCombatUpdate;
                _pluginInterface.Framework.OnUpdateEvent -= FrameworkInCombatUpdate;
                return;
            }

            var cooldowns =
                _debugTriggers.Select(t => (t, _getActionCooldownSlot(_actionManager, t.ActionCooldownGroup - 1)));

            
            foreach (var valueTuple in cooldowns.Where(t => t.Item2))
            {
                if (_currentTrigger == valueTuple.t)
                {
                    _currentEnumerator!.Dispose();
                    _currentEnumerator = null;
                    _currentTrigger = null;
                    _ffxivSetState(_maybeControllerStruct, 0, 0);
                }
                valueTuple.t.ShouldBeTriggered = true;

            }
            var filtered = cooldowns.Where(t => !t.Item2 && t.t.ShouldBeTriggered && t.t != _currentTrigger);
            
            _queue.EnqueueRange(filtered.Select(t => (t.t, t.t.Priority)));
            var priority = -1;
            var trigger = default(VibrationCooldownTrigger);
            if (_queue.TryPeek(out trigger, out priority))
            {
                if ((_currentTrigger?.Priority ?? MinValue) > priority)
                {
                    _currentEnumerator?.Dispose();
                    _currentTrigger = _queue.Dequeue();
                    _currentEnumerator = _currentTrigger.Pattern.GetEnumerator();
                    _currentTrigger.ShouldBeTriggered = _currentTrigger.Pattern.Infinite;
                    _ffxivSetState(_maybeControllerStruct, 0, 0);
                }
                PluginLog.Warning($"Heap element Prio: {priority}, currentElementPrio: {_currentTrigger?.Priority}");
            }

            if (_currentEnumerator is not null)
            {
                if (_currentEnumerator.MoveNext())
                {
                    var s = _currentEnumerator.Current;
                    if (s is not null)
                    {
                        _ffxivSetState(_maybeControllerStruct, s.RightMotorPercentage, s.LeftMotorPercentage);
                    }
                }
                else
                {
                    _ffxivSetState(_maybeControllerStruct, 0, 0);
                    _currentEnumerator.Dispose();
                    _currentEnumerator = null;
                    _currentTrigger = null;
                }
            }
        }
        private void FrameworkOutOfCombatUpdate(Framework framework)
        {
            
            // TODO !
            var inCombat = this._pluginInterface.ClientState.LocalPlayer.IsStatus(StatusFlags.InCombat);
            if (!inCombat) return;
            _pluginInterface.Framework.OnUpdateEvent += FrameworkInCombatUpdate;
            _pluginInterface.Framework.OnUpdateEvent -= FrameworkOutOfCombatUpdate;


        }

        private int ControllerPollDetour(nint maybeControllerStruct)
        {
            _maybeControllerStruct = maybeControllerStruct;
            #if DEBUG
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
                                  ConfigurationUi.DrawConfigUi(ref _config, _pluginInterface.SavePluginConfig);
            
            #if DEBUG
            DrawDebugUi();
            #endif
        }
        #if DEBUG
        private void DrawDebugUi()
        {
            ImGui.Begin($"{Constant.PluginName} Debug");
            ImGui.Text($"{_maybeControllerStruct.ToString("X12")}:{nameof(_maybeControllerStruct)}");
            ImGui.Text($"{nameof(ControllerPollDetour)} return Array (hex): ");
            foreach (var i in _lastReturnedFromPoll)
            {
                ImGui.SameLine();
                ImGui.Text($"{i:X}");
            }
            ImGui.Text($"{Marshal.ReadInt32(_maybeControllerStruct,0x88):X}:int (hex) at {nameof(_maybeControllerStruct)}+0x88");
            ImGui.Separator();
            ImGui.Text("First Pattern Name: "); ImGui.SameLine(); ImGui.Text($"{_config.Patterns[0]?.Name}");
            
            ImGui.Separator();
            ImGui.InputInt("RightMotorSpeed", ref _rightMotorSpeed);
            ImGui.InputInt("LeftMotorSpeed", ref _leftMotorSpeed);
            ImGui.InputInt("Cooldown Group", ref _cooldownGroup);
            if (ImGui.Button("FFXIV SetState"))
            {
                _ffxivSetState(_maybeControllerStruct, _rightMotorSpeed, _leftMotorSpeed);
            }

            if (ImGui.Button("XInput Wrapper SetSate"))
            {
                var state = new XInputVibration((ushort) _leftMotorSpeed, (ushort) _rightMotorSpeed);
                _xInputWrapperSetState(0, ref state);
            }

            if (ImGui.Button("Stop Vibration"))
            {
                _ffxivSetState(_maybeControllerStruct, 0, 0);
            }
            ImGui.Separator();
            //var cooldown = new Cooldown(0, 9999,-1,-1);
            // NOTE (Chiv): 58 is GCD
            var cooldown = _getActionCooldownSlot(_actionManager, _cooldownGroup - 1);
            ImGui.Text($"Cooldown Elapsed: {cooldown.CooldownElapsed}");
            ImGui.Text($"Cooldown Total: {cooldown.CooldownTotal}");
            ImGui.Text($"IsCooldown: {cooldown.IsCooldown}");
            ImGui.Text($"ActionID: {cooldown.ActionID}");
            ImGui.End();
        }
        #endif
        

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
            PluginLog.Warning($"Disposing: {disposing}");
            if (_isDisposed) return;
            if (disposing)
            {
                _pluginInterface.UiBuilder.OnOpenConfigUi -= OnOpenConfigUi;
                _pluginInterface.UiBuilder.OnBuildUi -= BuildUi;
                _pluginInterface.Framework.OnUpdateEvent -= FrameworkOutOfCombatUpdate;
                _pluginInterface.Framework.OnUpdateEvent -= FrameworkInCombatUpdate;
                _pluginInterface.CommandManager.RemoveHandler(Command);
                // TODO TESTING
                
                
                // TODO TESTING END
            }

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