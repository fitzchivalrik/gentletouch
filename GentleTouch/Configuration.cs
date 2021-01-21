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

        public bool ShouldVibrateDuringPvP { get; init; }

        public bool ShouldVibrateWithSheathedWeapon { get; init; }

        public bool ShouldVibrateDuringCasting { get; init; }

        public List<VibrationPattern> Patterns { get; init; } = new();

    }
}
