using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Internal;
using Dalamud.Hooking;
using Dalamud.Plugin;
using GentleTouch.Attributes;
using GentleTouch.Caraxi;
using Action = Lumina.Excel.GeneratedSheets.Action;

namespace GentleTouch
{
    public unsafe class GentleTouchPlugin : IDalamudPlugin
    {
        private DalamudPluginInterface _pluginInterface = null!;
        private PluginCommandManager<GentleTouchPlugin> _commandManager = null!;
        private Configuration _config = null!;
        private PluginUI _ui = null!;

        public string Name => "GentleTouch";

        private delegate void XInputVibrationPrototype(void* InputStruct, int rightMotorSpeed, int leftMotorSpeed);

        private Hook<XInputVibrationPrototype> ControllerVibration;
        private string sigBrBr = "40 55 53 56 48 8b ec 48 81 ec 80 00 00 00 33 f6 44 8b d2 4c 8b c9";
        private string sigBrBrAlt = "40 ? 53 56 48 8B ? 48 81 EC";
            

        private delegate void** GamePadConstructorPrototype(void** param_1, void* param_2);

        private Hook<GamePadConstructorPrototype> GamePadCtor;

        private string sigCtor =
            "48 89 5c 24 ?? 48 89 6c 24 ?? 48 89 74 24 ?? 48 89 7c 24 ?? 41 56 48 83  ec 20 48 8d 05 ?? ?? ?? ?? 4c 8b f1 48 89 01 48 8b ea 48 83 c1 08";

        private string sigCtorAlt =
            "48 89 ? ? ? 48 89 ? ? ? 48 89 ? ? ? 48 89 ? ? ? 41 ? 48 83 EC ? 48 8D ? ? ? ? ? 4C 8B ? 48 89 ? 48 8B";

        public delegate int PadPoll(void* InputStruct);

        private Hook<PadPoll> PadPoller;
        private string sigPadPoll =    "40 ?? 57 41 ?? 48 81 EC ?? ?? ?? ?? 44 0F ?? ?? ?? ?? ?? ?? ?? 48 8B";
        private string sigPadPollAlt = "40 56 57 41 56 48 81 EC ?? ?? ?? ?? 44 0F 29 84 24 ?? ?? ?? ??";
        [StructLayout(LayoutKind.Explicit, Size = 0x4)]
        public struct XinputVibration
        {
            [FieldOffset(0x0)] public ushort wLeftMotorSpeed;
            [FieldOffset(0x2)] public ushort wRightMotorSpeed;
        }

        private delegate int SetStateWrapperPrototype(int dwUserIndex, XinputVibration* pVibration);

        private Hook<SetStateWrapperPrototype> SetStateWrapper;
        private string sigWrapper = "48 ff 25 69 28 ce 01 cc cc cc cc cc cc cc cc cc 48 89 5c 24";

        private string sigWrapperAlt =
            "48 FF ? ? ? ? ? CC CC CC CC CC CC CC CC CC 48 89 ? ? ? 48 89 ? ? ? 48 89 ? ? ? 48 89";
        
        
        private delegate IntPtr StartCooldownDelegate(IntPtr actionManager, uint actionType, uint actionId);
        private Hook<StartCooldownDelegate> startCooldownHook;


        public void* inputst = null;
        public void** inputstCtor = null;
        public void* inputStCtorDe = null;
        public IntPtr actionManagerAddress = IntPtr.Zero;
        
        public Action? aCooldownAction;
        public uint LastActionId;
        
        internal Common Common;
        
        


        public void Initialize(DalamudPluginInterface pi)
        {
            this._pluginInterface = pi;

            this._config = (Configuration) this._pluginInterface.GetPluginConfig() ?? new Configuration();
            this._config.Initialize(this._pluginInterface);
            
            Common = new Common(pi);

            IntPtr XInputVibrationPtr = pi.TargetModuleScanner.ScanText(sigBrBr);
            ControllerVibration =
                new Hook<XInputVibrationPrototype>(XInputVibrationPtr, (XInputVibrationPrototype) XInputVibrationDetour);
            ControllerVibration.Enable();

            IntPtr padCtorPtr = pi.TargetModuleScanner.ScanText(sigCtor);
            GamePadCtor =
                new Hook<GamePadConstructorPrototype>(padCtorPtr,
                    (GamePadConstructorPrototype) GamePadConstructorDetour);
            GamePadCtor.Enable();

            IntPtr setStateWrapperPtr = pi.TargetModuleScanner.ScanText(sigWrapper);
            SetStateWrapper =
                new Hook<SetStateWrapperPrototype>(setStateWrapperPtr, (SetStateWrapperPrototype) SetStateWrapperDetour);
            SetStateWrapper.Enable();

            IntPtr padPollPtr = pi.TargetModuleScanner.ScanText(sigPadPoll);
            PadPoller = new Hook<PadPoll>(padPollPtr, (PadPoll) PadPollDetour);
            PadPoller.Enable();

            // ACTIONS STRAIGHT OUTTA REMINDME
            IEnumerable<Action> PlayerActions = pi.Data.Excel.GetSheet<Action>().Where(a => a.IsPlayerAction);
            aCooldownAction = PlayerActions.First(a =>
                a.CooldownGroup == 58 && !a.IsPvP &&
                a.ClassJobCategory.Value.HasClass(pi.ClientState.LocalPlayer?.ClassJob.Id ?? 6));

            actionManagerAddress = pi.TargetModuleScanner.GetStaticAddressFromSig("E8 ?? ?? ?? ?? 33 C0 E9 ?? ?? ?? ?? 8B 7D 0C");
            var ActionId = aCooldownAction.RowId;
            
            
            var startActionCooldownScan = pi.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? FF 50 18");
            startCooldownHook = new Hook<StartCooldownDelegate>(startActionCooldownScan, (StartCooldownDelegate)StartCooldownDetour);
            startCooldownHook.Enable();


            pi.Framework.OnUpdateEvent += this.FrameworkOnUpdate;
            
            this._ui = new PluginUI(this._config, this);
            this._pluginInterface.UiBuilder.OnBuildUi += this._ui.Draw;


            this._commandManager = new PluginCommandManager<GentleTouchPlugin>(this, this._pluginInterface);
        }

        private void FrameworkOnUpdate(Framework framework)
        {
            var cooldown = new Cooldown {CooldownElapsed = -1, CooldownTotal = -1, IsCooldown = 0, ActionID = 9999};
            // 58 is  GCD
            var cool = Common.ActionManager.GetActionCooldownSlot(aCooldownAction?.CooldownGroup ?? 0);
            if (cool != null)
            {
                cooldown = *cool;
            }

            var inCombat = this._pluginInterface.ClientState.LocalPlayer.IsStatus(StatusFlags.InCombat);
            if (!inCombat)
            {
                brbr(0, 0);
                return;
            }
            if (cooldown.IsCooldown == 0)
            {
                brbr(75,75);
                //brbr(0,0);
            }
            else if ( (cooldown.CooldownTotal  - cooldown.CooldownElapsed) < 0.4)
            {
                brbr(0,50);
                //brbr(0,0);
            }
            else
            {
                brbr(0,0);
            }
        }
        
        private IntPtr StartCooldownDetour(IntPtr actionManager, uint actionType, uint actionId) {
            PluginLog.Log($"StartCooldownDetour, actionID: {actionId}");
            PluginLog.Log($"StartCooldownDetour, actionType: {actionType}");
            LastActionId = actionId;
            return startCooldownHook.Original(actionManager, actionType, actionId);
        }

        private int PadPollDetour(void* inputstruct)
        {
            this.inputst = inputstruct;
            PluginLog.Log("STILL HOOKING THE POOLLER?");
            PadPoller.Disable();
            return PadPoller.Original(inputstruct);
        }

        private int SetStateWrapperDetour(int dwuserindex, XinputVibration* pvibration)
        {
            var e = SetStateWrapper.Original(dwuserindex, pvibration);
            PluginLog.Log(
                $"UserIndex {dwuserindex}, LeftMotor: {pvibration->wLeftMotorSpeed}, RightMotor {pvibration->wRightMotorSpeed}");
            PluginLog.Log($"Return: {e}");
            return e;
        }

        private void** GamePadConstructorDetour(void** param_1, void* param_2)
        {
            PluginLog.Log($"Ctor param_1 {new IntPtr(param_1).ToString("x8")}");
            PluginLog.Log($"Ctor *param_1 {new IntPtr(*param_1).ToString("x8")}");
            PluginLog.Log($"Ctor Param_2 {new IntPtr(param_2).ToString("x8")}");
            var p = GamePadCtor.Original(param_1, param_2);
            PluginLog.Log($"Ctor RETURN  {new IntPtr(p).ToString("x8")}");
            PluginLog.Log($"Ctor *RETURN {new IntPtr(*p).ToString("x8")}");
            this.inputstCtor = p;
            this.inputStCtorDe = *p;
            return p;
        }

        private void XInputVibrationDetour(void* InputStruct, int rightMotorSpeed, int leftMotorSpeed)
        {
            this.inputst = InputStruct;
            PluginLog.Log($"InputStruct {new IntPtr(InputStruct).ToString("x8")}");
            PluginLog.Log($"LeftMotor {leftMotorSpeed} ; Right Motor {rightMotorSpeed}");
            this.ControllerVibration.Original(InputStruct, rightMotorSpeed, leftMotorSpeed);
        }

        public void brbr(int rightMotorSpeed, int leftMotorSpeed)
        {
            if (this.inputst == null) return;

            this.ControllerVibration.Original(inputst, rightMotorSpeed, leftMotorSpeed);
        }

        public void brbrDirect(int rightMotorSpeed, int leftMotorSpeed)
        {
            var vibStr = new XinputVibration
            {
                wLeftMotorSpeed = (ushort) leftMotorSpeed,
                wRightMotorSpeed = (ushort) rightMotorSpeed
            };
            this.SetStateWrapper.Original(0, &vibStr);
        }

        [Command("/brbr")]
        [HelpMessage("Example help message.")]
        // ReSharper disable once UnusedMember.Global
        public void ExampleCommand1(string command, string args)
        {
            this._ui.IsVisible = true;
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            this.ControllerVibration.Disable();
            this.ControllerVibration.Dispose();
            this.GamePadCtor.Disable();
            this.GamePadCtor.Dispose();
            this.SetStateWrapper.Disable();
            this.SetStateWrapper.Dispose();
            this.PadPoller.Disable();
            this.PadPoller.Dispose();
            this._commandManager.Dispose();
            this.startCooldownHook.Disable();
            this.startCooldownHook.Dispose();

            this._pluginInterface.SavePluginConfig(this._config);

            this._pluginInterface.UiBuilder.OnBuildUi -= this._ui.Draw;
            this._pluginInterface.Framework.OnUpdateEvent -= this.FrameworkOnUpdate;

            this._pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}