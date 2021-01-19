using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using GentleTouch.Caraxi;
using ImGuiNET;

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
        private readonly Configuration _config;

        // TODO TESTING
        private int _rightMotorSpeed = 0;
        private int _leftMotorSpeed = 0;
        private int _cooldownGroup = 58;
        private int _nextStep = 0;
        private long _nextTimeStep = 0;

        private bool reset = false;
        //TODO END TESTING


        private nint _maybeControllerStruct;
        private bool _shouldDrawConfigUi =
#if DEBUG
                false
#else
            false
#endif
            ;

        private bool _isDisposed;
        


        public GentleTouch(DalamudPluginInterface pi, Configuration c)
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
            
            _pluginInterface = pi;
            _config = c;
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
                _ffxivSetState(_maybeControllerStruct, 0, 0);
                _pluginInterface.Framework.OnUpdateEvent += FrameworkOutOfCombatUpdate;
                _pluginInterface.Framework.OnUpdateEvent -= FrameworkInCombatUpdate;
                return;
            }
            var pattern = new[]
            {
                new VibrationStepStruct(50, 50,50),
                new VibrationStepStruct(0, 0,50),
            };

            var sfs = new VibrationPattern.Step(
                50,
                70,
                100);
            

            var cooldown = _getActionCooldownSlot(_actionManager, _cooldownGroup-1);
            if (cooldown)
            {
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > _nextTimeStep)
                {
                    var p = pattern[_nextStep++ % pattern.Length];
                    _nextTimeStep = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + p.MillisecondsTillNextStep;
                    PluginLog.Log($"Pattern for {_cooldownGroup}: {p}");
                    _ffxivSetState(_maybeControllerStruct, p.RightMotorPercentage, p.LeftMotorPercentage);
                }
            }
            else
            {
                _ffxivSetState(_maybeControllerStruct, 0, 0);
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
            _controllerPoll.Disable();
            return _controllerPoll.Original(maybeControllerStruct);
        }

        #region UI

        private void BuildUi()
        {
            _shouldDrawConfigUi = _shouldDrawConfigUi && DrawConfigUi(_config, _pluginInterface.SavePluginConfig);
            
            #if DEBUG
            ImGui.Begin($"{Constant.PluginName} Debug");
            if (ImGui.Button("FFXIV SetState"))
            {
                _ffxivSetState(_maybeControllerStruct, _rightMotorSpeed, _leftMotorSpeed);
            }

            if (ImGui.Button("XInput Wrapper SetSate"))
            {
                var state = new XInputVibration((ushort) _leftMotorSpeed, (ushort) _rightMotorSpeed);
                _xInputWrapperSetState(0,ref state);
            }

            if (ImGui.Button("Stop Vibration"))
            {
                _ffxivSetState(_maybeControllerStruct, 0, 0);
            }
            ImGui.InputInt("RightMotorSpeed", ref _rightMotorSpeed);
            ImGui.InputInt("LeftMotorSpeed", ref _leftMotorSpeed);
            ImGui.InputInt("Cooldown Group", ref _cooldownGroup);
            ImGui.Separator();
            //var cooldown = new Cooldown(0, 9999,-1,-1);
            // NOTE (Chiv): 58 is GCD
            var cooldown = _getActionCooldownSlot(_actionManager, _cooldownGroup-1);
            ImGui.Text($"Cooldown Elapsed: {cooldown.CooldownElapsed}");
            ImGui.Text($"Cooldown Total: {cooldown.CooldownTotal}");
            ImGui.Text($"IsCooldown: {cooldown.IsCooldown}");
            ImGui.Text($"ActionID: {cooldown.ActionID}");
            ImGui.End();
            #endif
        }

        private static bool DrawConfigUi(Configuration config, Action<IPluginConfiguration> saveConfiguration)
        {
            var shouldDrawConfigUi = true;
            var scale = ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale),
                new Vector2(600 * scale, 800 * scale));
            ImGui.Begin($"{Constant.PluginName} Configuration", ref shouldDrawConfigUi, ImGuiWindowFlags.NoCollapse);
            if (ImGui.Checkbox(nameof(config.ShouldVibrateDuringPvP), ref config.ShouldVibrateDuringPvP))
                saveConfiguration(config);
            if (ImGui.Checkbox(nameof(config.ShouldVibrateDuringCasting), ref config.ShouldVibrateDuringCasting))
                saveConfiguration(config);
            if (ImGui.Checkbox(nameof(config.ShouldVibrateWithSheathedWeapon),
                ref config.ShouldVibrateWithSheathedWeapon)) saveConfiguration(config);

            ImGui.End();
            return shouldDrawConfigUi;
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
            PluginLog.Warning($"Disposing: {disposing}");
            if (_isDisposed) return;
            if (disposing)
            {
                _pluginInterface.UiBuilder.OnOpenConfigUi -= OnOpenConfigUi;
                _pluginInterface.UiBuilder.OnBuildUi -= BuildUi;
                _pluginInterface.Framework.OnUpdateEvent -= FrameworkOutOfCombatUpdate;
                _pluginInterface.Framework.OnUpdateEvent -= FrameworkInCombatUpdate;
                _pluginInterface.CommandManager.RemoveHandler(Command);
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