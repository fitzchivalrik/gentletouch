using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Plugin;

namespace GentleTouch
{
    public class GentleTouchBridge : IDalamudPlugin
    {
        public string Name => Constant.PluginName;
        private GentleTouch _plugin = null!;

        public void Initialize(DalamudPluginInterface pi)
        {
            var config = pi.GetPluginConfig() as Configuration ?? new Configuration();
            _plugin = new GentleTouch(pi, config);
        }

        public void Dispose()
        {
            _plugin.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}