using Dalamud.Game;
using Dalamud.Plugin;
using GentleTouch.Caraxi;

namespace GentleTouch
{
    public class Common
    {
        public static DalamudPluginInterface PluginInterface { get; private set; }
        public static SigScanner Scanner => PluginInterface.TargetModuleScanner;
        
        private static ActionManager _actionManager;
        public static ActionManager ActionManager {
            get {
                if (_actionManager != null) return _actionManager;
                var address = PluginInterface.TargetModuleScanner.GetStaticAddressFromSig("E8 ?? ?? ?? ?? 33 C0 E9 ?? ?? ?? ?? 8B 7D 0C");
                _actionManager = new ActionManager(address);
                return _actionManager;
            }
        }
        
        public Common(DalamudPluginInterface pluginInterface) {
            PluginInterface = pluginInterface;
            //var gameAllocPtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 45 8D 67 23");
            //var getGameAllocatorPtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 75 08");

            //_inventoryManager = pluginInterface.TargetModuleScanner.GetStaticAddressFromSig("BA ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F8 48 85 C0");
            //var getInventoryContainerPtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 55 BB");
            //var getContainerSlotPtr = pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 8B 5B 0C");

            //PlayerStaticAddress = pluginInterface.TargetModuleScanner.GetStaticAddressFromSig("8B D7 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 0F B7 E8");

            //_gameAlloc = Marshal.GetDelegateForFunctionPointer<GameAlloc>(gameAllocPtr);
            //_getGameAllocator = Marshal.GetDelegateForFunctionPointer<GetGameAllocator>(getGameAllocatorPtr);

        }
    }
}