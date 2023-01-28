using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Dalamud.Interface;
using GentleTouch.Triggers;
using ImGuiNET;
using Newtonsoft.Json;

namespace GentleTouch.UI;

internal partial class Config
{
    private static bool DrawPatternTab(Configuration config, float scale, ref IEnumerator<VibrationPattern.Step?>? patternEnumerator)
    {
        if (!ImGui.BeginTabItem("Patterns")) return false;
        var          changed            = false;
        const string importExportPrefix = "GTP";
        if (ImGui.Button("Add new Pattern"))
        {
            config.Patterns.Add(new VibrationPattern());
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.Button("Import from clipboard"))
        {
            var prefixedBase64 = ImGui.GetClipboardText();
            try
            {
                if (prefixedBase64 is not null && prefixedBase64.StartsWith(importExportPrefix))
                {
                    var base64Bytes = Convert.FromBase64String(prefixedBase64.Substring(importExportPrefix.Length));
                    var json        = Encoding.UTF8.GetString(base64Bytes);
                    var p           = JsonConvert.DeserializeObject<VibrationPattern>(json);
                    if (p is not null)
                    {
                        p.Guid = Guid.NewGuid();
                        config.Patterns.Add(p);
                        changed = true;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        ImGui.Separator();
        var toRemovePatterns = new List<VibrationPattern>();
        foreach (var pattern in config.Patterns)
        {
            ImGui.PushID(pattern.Guid.GetHashCode());
            if (DrawDeleteButton(FontAwesomeIcon.TrashAlt))
            {
                toRemovePatterns.Add(pattern);
                changed = true;
            }

            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(FontAwesomeIcon.Play.ToIconString()))
            {
                var p = new VibrationPattern
                {
                    Cycles = pattern.Infinite ? 4 : pattern.Cycles, Infinite = false, Steps = pattern.Steps, Name = pattern.Name
                };
                patternEnumerator = p.GetEnumerator();
            }

            ImGui.PopFont();
            ImGui.PopStyleColor();
            if (pattern.Infinite && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Infinite pattern will be repeated 4 times.");
                ImGui.EndTooltip();
            }

            ImGui.SameLine();
            var open = ImGui.TreeNodeEx($"{pattern.Name}###{pattern.Guid}", ImGuiTreeNodeFlags.AllowItemOverlap | ImGuiTreeNodeFlags.Bullet);

            ImGui.SameLine(300 * scale);

            if (ImGui.Button("Export to clipboard"))
            {
                var jsonOutput = JsonConvert.SerializeObject(pattern);
                if (jsonOutput is not null)
                {
                    var prefixedBase64 =
                        $"{importExportPrefix}{Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonOutput))}";
                    ImGui.SetClipboardText(prefixedBase64);
                }
            }

            if (open)
            {
                changed |= DrawPatternGenerals(pattern, scale);
                changed |= DrawPatternSteps(pattern, scale);
            }

            ImGui.Separator();
            ImGui.PopID();
        }

        foreach (var pattern in toRemovePatterns)
        {
            config.Patterns.Remove(pattern);
        }

        ImGui.EndTabItem();
        return changed;
    }

    private static bool DrawPatternSteps(VibrationPattern pattern, float scale)
    {
        var changed = false;
        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{FontAwesomeIcon.Plus.ToIconString()}##PatternStep",
                new Vector2(23 * scale, 23 * scale)))
        {
            pattern.Steps.Add(new VibrationPattern.Step(0, 0));
            changed = true;
        }

        ImGui.PopFont();
        var toRemoveSteps = new List<int>();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Steps");
        for (var i = 0; i < pattern.Steps.Count; i++)
        {
            ImGui.PushID(i);
            var s = pattern.Steps[i];
            ImGui.AlignTextToFramePadding();
            ImGui.Text($"{i + 1}");
            ImGui.SameLine();
            changed |= DrawHorizontalStep(scale, s);
            ImGui.SameLine();
            if (DrawDeleteButton(FontAwesomeIcon.TrashAlt,
                    new Vector2(23 * scale, 23 * scale),
                    "Delete this Step."))
            {
                toRemoveSteps.Add(i);
                changed = true;
            }

            ImGui.PopID();
        }

        foreach (var i in toRemoveSteps)
        {
            pattern.Steps.RemoveAt(i);
        }

        ImGui.TreePop();
        return changed;
    }

    private static bool DrawPatternGenerals(VibrationPattern pattern, float scale)
    {
#if DEBUG
            ImGui.Text($"UUID: {pattern.Guid}");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) ImGui.SetClipboardText(pattern.Guid.ToString());
#endif
        var changed = false;
        ImGui.SetNextItemWidth(175 * scale);
        var n = pattern.Name == "Nameless" ? "" : pattern.Name;
        if (ImGui.InputTextWithHint("##Pattern Name", "Name of Pattern", ref n, 20) && n.Trim() != "")
        {
            pattern.Name = n;
            changed      = true;
        }

        ImGui.SameLine();
        if (ImGui.Checkbox("Infinite", ref pattern.Infinite)) changed = true;
        if (pattern.Infinite) return changed;
        ImGui.SameLine();
        ImGui.SetNextItemWidth(75 * scale);
        if (ImGui.InputInt("Cycles", ref pattern.Cycles, 1)) changed = true;
        return changed;
    }

    private static bool DrawHorizontalStep(float scale, VibrationPattern.Step s)
    {
        var changed = false;
        ImGui.PushItemWidth(150 * scale);
        if (ImGui.SliderInt("##Left Slider", ref s.LeftMotorPercentage, 0, 100, "Left Motor %d %%", ImGuiSliderFlags.AlwaysClamp))
        {
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.SliderInt("##Right Slider", ref s.RightMotorPercentage, 0, 100, "Right Motor %d %%", ImGuiSliderFlags.AlwaysClamp))
        {
            changed = true;
        }

        ImGui.SameLine();
        if (ImGui.DragInt("##MS Drag", ref s.MillisecondsTillNextStep, 10, 0, 2000, "%d ms till next"))
        {
            changed = true;
        }

        ImGui.PopItemWidth();
        return changed;
    }
}