using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public unsafe class GentleTouchExploration
    {
        private DalamudPluginInterface _pluginInterface = null!;
        private PluginCommandManager<GentleTouchExploration> _commandManager = null!;
        private Configuration _config = null!;

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
        public int counter = 0;
        
        internal Common Common;
        
        public float timeElapsed = 0.0f;
        public long switchTime = 100;
        public bool quiet = false;
        public bool started = false;
        public Stopwatch mywatch = new  Stopwatch();
        public int[] values = {50,100};
        public int current = 0;
        
        


        public void Initialize(DalamudPluginInterface pi)
        {
            this._pluginInterface = pi;

            this._config = this._pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            
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



            this._commandManager = new PluginCommandManager<GentleTouchExploration>(this, this._pluginInterface);
        }

        private void FrameworkOnUpdate(Framework framework)
        {
            var cooldown = new Cooldown(0, 9999,-1,-1);
            // 58 is  GCD
            var cool = Common.ActionManager.GetActionCooldownSlot(aCooldownAction?.CooldownGroup ?? 0);
            if (cool != null)
            {
                cooldown = *cool;
            }

            var inCombat = this._pluginInterface.ClientState.LocalPlayer.IsStatus(StatusFlags.InCombat);
            if (cooldown.IsCooldown == 1)
            {
                counter++;
            }
            if (!inCombat)
            {
                if (started)
                {
                    started = false;
                    brbr(0, 0);
                }

                return;
            }
            if (cooldown.IsCooldown == 0)
            {
                //brbr(75,75);
                if (!started)
                {
                    mywatch.Restart();
                    started = true;
                    brbr(values[current++ % values.Length],values[current++ % values.Length]);
                }
                else if (mywatch.ElapsedMilliseconds > switchTime)
                {
                    mywatch.Restart();
                    brbr(values[current++ % values.Length], values[current++ % values.Length]);
                }
            }
            //else if ( (cooldown.CooldownTotal  - cooldown.CooldownElapsed) < 0.4)
            //{
            //    brbr(0,50);
            //    //brbr(0,0);
            //}
            else
            {
                started = false;
                brbr(0,0);
            }

     
        }
        
        private IntPtr StartCooldownDetour(IntPtr actionManager, uint actionType, uint actionId) {
            PluginLog.Log($"StartCooldownDetour, actionID: {actionId}");
            PluginLog.Log($"StartCooldownDetour, actionType: {actionType}");
            LastActionId = actionId;
            //var action = aCooldownAction;
            var action = this._pluginInterface.Data.Excel.GetSheet<Action>()
                .FirstOrDefault(a => a.IsPlayerAction && a.RowId == actionId);
            if (action == null)
            {
                var ac = this._pluginInterface.Data.Excel.GetSheet<Action>()
                    .FirstOrDefault(a => a.RowId == actionId);
                PluginLog.Log($"Action {ac.RowId}:{ac.Name} Playeraction: {ac.IsPlayerAction } ");
                PluginLog.Log($"Cooldowngroup: {ac.CooldownGroup}");
            }
            var cool = Common.ActionManager.GetActionCooldownSlot(action?.CooldownGroup ?? 58);
            if (cool != null && cool->ActionID != 0)
            {
                PluginLog.Log($"Cooldown Elapsed: {cool->CooldownElapsed}");
                PluginLog.Log($"Cooldown Total: {cool->CooldownTotal}");
                PluginLog.Log($"IsCooldown: {cool->IsCooldown}");
                PluginLog.Log($"ActionID: {cool->ActionID}");
            }
            else
            {
                PluginLog.Log($"No cooldown for group {action?.CooldownGroup}");
            }
            var r =  startCooldownHook.Original(actionManager, actionType, actionId);
            cool = Common.ActionManager.GetActionCooldownSlot(action?.CooldownGroup ?? 58);
            if (cool != null && cool->ActionID != 0)
            {
                PluginLog.Log($"Cooldown Elapsed: {cool->CooldownElapsed}");
                PluginLog.Log($"Cooldown Total: {cool->CooldownTotal}");
                PluginLog.Log($"IsCooldown: {cool->IsCooldown}");
                PluginLog.Log($"ActionID: {cool->ActionID}");
            }
            else
            {
                PluginLog.Log($"No cooldown for group {action?.CooldownGroup}");
            }
            return r;
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
            //PluginLog.Log($"InputStruct {new IntPtr(InputStruct).ToString("x8")}");
            //PluginLog.Log($"LeftMotor {leftMotorSpeed} ; Right Motor {rightMotorSpeed}");
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
            //this._ui.IsVisible = true;
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