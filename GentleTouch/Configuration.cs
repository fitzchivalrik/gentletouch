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

        public bool RisksAcknowledged;
        public bool OptForNoUsage;

        public IList<VibrationPattern> Patterns = new List<VibrationPattern>();

        public IList<VibrationCooldownTrigger> CooldownTriggers = new List<VibrationCooldownTrigger>();

    }
}
