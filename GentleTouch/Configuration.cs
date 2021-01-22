using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Game.ClientState;
using Dalamud.Plugin;
using ImGuiNET;
using Newtonsoft.Json;

namespace GentleTouch
{
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; }

        public bool ShouldVibrateDuringPvP;

        public bool OnlyVibrateWithDrawnWeapon;

        public bool ShouldVibrateDuringCasting;

        public List<VibrationPattern> Patterns = new();

    }
}
