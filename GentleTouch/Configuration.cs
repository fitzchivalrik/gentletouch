using System.Collections.Generic;
using Dalamud.Configuration;

namespace GentleTouch
{
    public class Configuration : IPluginConfiguration
    {
        public IList<VibrationCooldownTrigger> CooldownTriggers = new List<VibrationCooldownTrigger>();
        public IList<VibrationPattern> Patterns = new List<VibrationPattern>();

        public bool RisksAcknowledged;
        
        public bool NoVibrationWithSheathedWeapon;
        public bool NoVibrationDuringCasting;

        public int Version { get; set; }
    }
}