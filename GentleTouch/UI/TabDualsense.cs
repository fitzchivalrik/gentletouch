using GentleTouch.Interop.DualSense;
using ImGuiNET;

namespace GentleTouch.UI;

internal partial class Config
{
    private static bool DrawPlaystationTab(Configuration config, float scale)
    {
        var changed = false;
        if (!ImGui.BeginTabItem("DualSense/Shock")) return changed;
        ImGui.TextWrapped("NOTE: If the DualSense works with the game (haptics and sound output)," +
                          " but there are no vibrations from triggers," +
                          " press the 'Mute Mic' button on the controller twice.\n" +
                          "Not fixed? Sorry, please restart the Game, with only the DualSense attached to the PC.");
        ImGui.Separator();
        ImGui.TextWrapped("NOTE: The Create (DualSense) /TouchPad (DualShock4) button executes Individual Macro #96.\n" +
                          "The PS button executes Individual Macro #97.\n" +
                          "Works for DualShock4 and DualSense.");
        ImGui.Separator();
        // Maybe later
        // changed |= ImGui.Checkbox("Turn on light bar.", ref config.TurnLightBarOn);
        // changed |= ImGui.ColorPicker3("LightBar Colour", ref config.LightBarColour, ImGuiColorEditFlags.Uint8);
        // ImGui.Text($"Colour: {config.LightBarColour}");
        changed |= ImGui.Checkbox("/sheathe & /draw with PS button, instead of Individual Macro #97.", ref config.PsButtonDrawWeapon);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Needs to have '/sheathe' & '/draw' emotes unlocked! (Buy in the Gold Saucer.)");
            ImGui.EndTooltip();
        }

        changed |= ImGui.Checkbox("Enable legacy DualSense compatibility vibrations. (Stronger.)", ref config.LegacyDualSenseVibrations);

        changed |= ImGui.Checkbox("Enable DualSense adaptive trigger effect.",
            ref config.SetDualSenseAdaptiveTrigger);
        if (config.SetDualSenseAdaptiveTrigger)
        {
            ImGui.Indent();
            ImGui.TextWrapped("NOTE: If the trigger state is reset (e.g., because the game used them)," +
                              " execute '/gentle r', or change a configuration value, to re-init.");
            ImGui.Separator();
            ImGui.PushItemWidth(150 * scale);

            var effectType = (int)config.DualSenseAdaptiveTriggerType;
            ImGui.Text("Effect Type");
            changed |= ImGui.RadioButton("Continuous Resistance##GentleTouch", ref effectType, (int)AdaptiveTriggerEffectType.ContinuousResistance);
            ImGui.SameLine();
            changed |= ImGui.RadioButton("Section Resistance##GentleTouch", ref effectType, (int)AdaptiveTriggerEffectType.SectionResistance);
            if (changed) config.DualSenseAdaptiveTriggerType = (AdaptiveTriggerEffectType)effectType;

            var startPositionL2 = (int)config.TriggerL2StartPosition;
            var forceOrEndPosL2 = (int)config.TriggerL2ForceOrEndPosition;
            var startPositionR2 = (int)config.TriggerR2StartPosition;
            var forceOrEndPosR2 = (int)config.TriggerR2ForceOrEndPosition;

            changed |= ImGui.SliderInt(
                "###L2StartPosition",
                ref startPositionL2,
                0,
                100,
                "L2 Start %d %%",
                ImGuiSliderFlags.AlwaysClamp);
            ImGui.SameLine();
            changed |= ImGui.SliderInt(
                "###L2StartForce",
                ref forceOrEndPosL2,
                0,
                100,
                config.DualSenseAdaptiveTriggerType is AdaptiveTriggerEffectType.ContinuousResistance ? "L2 Force %d %%" : "L2 End %d %%",
                ImGuiSliderFlags.AlwaysClamp);
            changed |= ImGui.SliderInt(
                "###R2StartPosition",
                ref startPositionR2,
                0,
                100,
                "R2 Start %d %%",
                ImGuiSliderFlags.AlwaysClamp);
            ImGui.SameLine();
            changed |= ImGui.SliderInt(
                "###R2StartForce",
                ref forceOrEndPosR2,
                0,
                100,
                config.DualSenseAdaptiveTriggerType is AdaptiveTriggerEffectType.ContinuousResistance ? "R2 Force %d %%" : "R2 End %d %%",
                ImGuiSliderFlags.AlwaysClamp);

            config.TriggerL2StartPosition      = (byte)startPositionL2;
            config.TriggerL2ForceOrEndPosition = (byte)forceOrEndPosL2;
            config.TriggerR2StartPosition      = (byte)startPositionR2;
            config.TriggerR2ForceOrEndPosition = (byte)forceOrEndPosR2;
            ImGui.Unindent();
        } else
        {
            config.DualSenseAdaptiveTriggerType = AdaptiveTriggerEffectType.Default;
        }

        ImGui.EndTabItem();
        return changed;
    }
}