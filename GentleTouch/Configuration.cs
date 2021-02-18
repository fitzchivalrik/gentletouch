using System.Collections.Generic;
using Dalamud.Configuration;

namespace GentleTouch
{
    public class Configuration : IPluginConfiguration
    {
        public IList<VibrationCooldownTrigger> CooldownTriggers = new List<VibrationCooldownTrigger>();
        public IList<VibrationPattern> Patterns = new List<VibrationPattern>();

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
        AskAboutGCD,
        Done
    }
}