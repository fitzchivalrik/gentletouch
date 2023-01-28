using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using GentleTouch.Interop.DualSense;
using GentleTouch.Triggers;

namespace GentleTouch;

public class Configuration : IPluginConfiguration
{
    public readonly IList<CooldownTrigger>  CooldownTriggers = new List<CooldownTrigger>();
    public readonly IList<VibrationPattern> Patterns         = new List<VibrationPattern>();

    public bool LegacyDualSenseVibrations;
    public int  MaxAetherCurrentSenseDistanceSquared = 100 * 100;
    public bool NoVibrationDuringCasting;
    public bool NoVibrationWithSheathedWeapon;
    public bool SenseAetherCurrents;

    public Onboarding OnboardingStep;

    public AdaptiveTriggerEffectType DualSenseAdaptiveTriggerType;
    public bool                      SetDualSenseAdaptiveTrigger;
    public Vector3                   LightBarColour;
    public byte                      TriggerL2StartForce    = 0x00;
    public byte                      TriggerL2StartPosition = 0x00;
    public byte                      TriggerR2StartForce    = 0x00;
    public byte                      TriggerR2StartPosition = 0x00;
    public bool                      TurnLightBarOn;

    public int Version { get; set; } = 3;
}

public enum Onboarding
{
    TellAboutRisk
  , AskAboutExamplePatterns
  , AskAboutExampleCooldownTriggers
    // ReSharper disable once InconsistentNaming
  , AskAboutGCD
  , Done
}