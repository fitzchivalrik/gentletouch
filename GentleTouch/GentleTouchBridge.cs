using System;
using Dalamud.Plugin;

namespace GentleTouch
{
    public class GentleTouchBridge : IDalamudPlugin
    {
        private GentleTouch _plugin = null!;
        public string Name => GentleTouch.PluginName;

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