using System.Collections.Generic;
using Dalamud.Configuration;
using GentleTouch.Triggers;

namespace GentleTouch
{
    public class Configuration : IPluginConfiguration
    {
        public readonly IList<CooldownTrigger> CooldownTriggers = new List<CooldownTrigger>();
        public readonly IList<VibrationPattern> Patterns = new List<VibrationPattern>();
        
        public Onboarding OnboardingStep;
        public bool NoVibrationWithSheathedWeapon;
        public bool NoVibrationDuringCasting;
        public bool SenseAetherCurrents;
        public int MaxAetherCurrentSenseDistanceSquared = 100 * 100;

        public int Version { get; set; } = 2;
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