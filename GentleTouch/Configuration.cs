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
        public Onboarding OnboardingStep;

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