using Dalamud.Configuration;
using Dalamud.Game.ClientState;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace GentleTouch
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        // Add any other properties or methods here.
        [JsonIgnore] private DalamudPluginInterface pluginInterface;
        
        public bool HideKofi;
        public bool ShowExperimentalTweaks;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.pluginInterface.SavePluginConfig(this);
        }
    }
}
