using System;
using ImGuiNET;

namespace GentleTouch.UI;

internal partial class Config
{
    private static bool DrawGeneralTab(Configuration config, float scale)
    {
        if (!ImGui.BeginTabItem("General")) return false;
        var changed = ImGui.Checkbox("Disable cooldown trigger while casting.",
            ref config.NoVibrationDuringCasting);
        changed |= ImGui.Checkbox("Disable cooldown trigger while weapon is sheathed.",
            ref config.NoVibrationWithSheathedWeapon);
        changed |= ImGui.Checkbox("Sense Aether Currents (out of combat).", ref config.SenseAetherCurrents);
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.Text("Gradually stronger vibration the closer you are to an Aether Current.");
            ImGui.EndTooltip();
        }

        if (config.SenseAetherCurrents)
        {
            ImGui.Indent();
            ImGui.SetNextItemWidth(250 * scale);
            var distance = (int)Math.Sqrt(config.MaxAetherCurrentSenseDistanceSquared);
            changed |= ImGui.SliderInt("##AetherSenseDistance", ref distance, 5, 115,
                "Max Sense Distance %d");
            config.MaxAetherCurrentSenseDistanceSquared = distance * distance;
            ImGui.Unindent();
        }

        ImGui.EndTabItem();
        return changed;
    }
}