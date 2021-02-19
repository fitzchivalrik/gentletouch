using System.Collections.Generic;
using Dalamud.Configuration;
using GentleTouch.Triggers;

namespace GentleTouch
{
    public class Configuration : IPluginConfiguration
    {
        public readonly IList<CooldownTrigger> CooldownTriggers = new List<CooldownTrigger>();
        public readonly IList<VibrationPattern> Patterns = new List<VibrationPattern>();

        // TODO Remove in Version 2
        public bool RisksAcknowledged;
        
        public Onboarding OnboardingStep;
        public bool NoVibrationWithSheathedWeapon;
        public bool NoVibrationDuringCasting;
        public bool SenseAetherCurrents;
        public int MaxAetherCurrentSenseDistance = 100;

        public int Version { get; set; } = 1;
    }

    public enum Onboarding
    {
        TellAboutRisk,
        AskAboutExamplePatterns,
        AskAboutExampleCooldownTriggers,
        // ReSharper disable once InconsistentNaming
        AskAboutGCD,
        Done
    }
}