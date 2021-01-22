using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Configuration;
using Dalamud.Plugin;
using GentleTouch.Caraxi;
using ImGuiNET;

namespace GentleTouch
{
    internal static class ConfigurationUi
    {
        internal static bool DrawConfigUi(ref Configuration config, Action<IPluginConfiguration> save)
        {
            var shouldDrawConfigUi = true;
            var changed = false;
            var scale = ImGui.GetIO().FontGlobalScale;
            ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale),
                new Vector2(1200 * scale, 1000 * scale));
            ImGui.Begin($"{Constant.PluginName} Configuration", ref shouldDrawConfigUi, ImGuiWindowFlags.NoCollapse);
            if (ImGui.Checkbox(nameof(config.ShouldVibrateDuringPvP), ref config.ShouldVibrateDuringPvP))
                changed = true;
            if (ImGui.Checkbox(nameof(config.ShouldVibrateDuringCasting), ref config.ShouldVibrateDuringCasting))
                changed = true;
            if (ImGui.Checkbox(nameof(config.OnlyVibrateWithDrawnWeapon),
                ref config.OnlyVibrateWithDrawnWeapon)) changed = true;
            ImGui.Separator();
            if (ImGui.Button("A"))
            {
                config.Patterns.Add(new VibrationPattern());
                changed = true;
            }

            var toRemovePatterns = new List<VibrationPattern>();
            foreach (var pattern in config.Patterns)
            {
                if (ImGui.Button("X", new Vector2(23 * scale, 23 * scale)))
                {
                    ImGui.OpenPopup("Sure?");
                }
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    ImGui.Text("Delete this pattern.");
                    ImGui.EndTooltip();
                }

                if (ImGui.BeginPopup("Sure?"))
                {
                    ImGui.Text("Really delete?");
                    if (ImGui.Button("Yes"))
                    {
                        toRemovePatterns.Add(pattern);
                        changed = true;
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
                ImGui.SameLine();
                if (ImGui.TreeNode($"{pattern.Name}###{pattern.Guid}"))
                {
                    ImGui.PushID(pattern.Guid.GetHashCode());
#if DEBUG
                    ImGui.Text($"UUID: {pattern.Guid}");
                    if (ImGui.IsItemClicked(1))
                    {
                        ImGui.SetClipboardText(pattern.Guid.ToString());
                    }

                    if (ImGui.IsItemHovered())
                    {
                        //var s = ImGui.CalcTextSize("Right-Click to copy UUID to clipboard.");
                        //ImGui.SetNextWindowSize(new Vector2(s.X/2, -1));
                        //ImGui.SetNextWindowContentSize();
                        ImGui.BeginTooltip();
                        ImGui.Text($"Right-Click to copy UUID to clipboard.");
                        ImGui.EndTooltip();
                    }
#endif
                    ImGui.SetNextItemWidth(150);
                    if (ImGui.InputTextWithHint("Pattern Name", "Name of Pattern", ref pattern.Name, 32))
                        changed = true;
                    ImGui.SetNextItemWidth(75);
                    if (ImGui.InputInt($"Cycles", ref pattern.Cycles, 1)) changed = true;
                    ImGui.SameLine();
                    if (ImGui.Checkbox("Infinite", ref pattern.Infinite)) changed = true;
                    var toRemoveSteps = new List<int>();
                    if (ImGui.Button("A"))
                    {
                        pattern.Steps.Add(new VibrationPattern.Step(0,0));
                        changed = true;
                    }

                    //ImGui.BeginGroup();
                    for (var i = 0; i < pattern.Steps.Count; i++)
                    {
                        ImGui.PushID(i);
                        if (ImGui.Button("X", new Vector2(23 * scale, 23 * scale)))
                        {
                            ImGui.OpenPopup("Sure?");
                        }

                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            ImGui.Text("Delete this step.");
                            ImGui.EndTooltip();
                        }

                        if (ImGui.BeginPopup("Sure?"))
                        {
                            ImGui.Text("Really delete?");
                            if (ImGui.Button("Yes"))
                            {
                                toRemoveSteps.Add(i);
                                changed = true;
                                ImGui.CloseCurrentPopup();
                            }

                            ImGui.EndPopup();
                        }
                        ImGui.SameLine();
                        //ImGui.BeginChild($"{pattern.Guid}-Step-{i}");
                        if (ImGui.TreeNode($"Step {i + 1}"))
                        {
                            var s = pattern.Steps[i];
                            ImGui.PushItemWidth(150 * scale);
                            if (ImGui.SliderInt("##Left Slider", ref s.LeftMotorPercentage, 0, 100, "Left Motor %d %%"))
                                changed = true;
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text("Left Motor Strength in %%");
                                ImGui.EndTooltip();
                            }

                            ImGui.SameLine();
                            if (ImGui.SliderInt("##Right Slider", ref s.RightMotorPercentage, 0, 100,
                                "Right Motor %d %%"))
                                changed = true;
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text("Right Motor Strength in %%");
                                ImGui.EndTooltip();
                            }

                            ImGui.SameLine();
                            var centisecondsTillNextStep = s.MillisecondsTillNextStep / 10;
                            if (ImGui.InputInt($"##Centiseconds till next step", ref centisecondsTillNextStep, 1))
                            {
                                s.MillisecondsTillNextStep = centisecondsTillNextStep * 10;
                                changed = true;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text("Centiseconds till next step");
                                ImGui.EndTooltip();
                            }

                            ImGui.PopItemWidth();

                            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
                            if (ImGui.Button("X", new Vector2(23 * scale, 23 * scale)))
                            {
                                ImGui.OpenPopup("Sure?2");
                            }

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text("Delete this step.");
                                ImGui.EndTooltip();
                            }

                            if (ImGui.BeginPopup("Sure?2"))
                            {
                                ImGui.Text("Really delete?");
                                if (ImGui.Button("Yes"))
                                {
                                    toRemoveSteps.Add(i);
                                    changed = true;
                                    ImGui.CloseCurrentPopup();
                                }

                                ImGui.EndPopup();
                            }
                            ImGui.SameLine();
                            ImGui.SetNextItemWidth(32 * scale);
                            if (ImGui.InputInt($"##centi", ref centisecondsTillNextStep, 0))
                            {
                                s.MillisecondsTillNextStep = centisecondsTillNextStep * 10;
                                changed = true;
                            }
                            //if (ImGui.DragInt("##milli", ref s.MillisecondsTillNextStep, 10, 10, 5000)) changed = true;
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text("Centiseconds till next step.");
                                ImGui.EndTooltip();
                            }

                            ImGui.PopStyleVar();
                            if (ImGui.VSliderInt("##leftMotorPercentage", new Vector2(25 * scale, 50 * scale),
                                ref s.LeftMotorPercentage, 0,
                                100))
                                changed
                                    = true;
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text("Left Motor Strength in %%");
                                ImGui.EndTooltip();
                            }

                            ImGui.SameLine();
                            if (ImGui.VSliderInt("##rightMotorPercentage", new Vector2(25 * scale, 50 * scale),
                                ref s.RightMotorPercentage,
                                0, 100))
                                changed
                                    = true;
                            if (ImGui.IsItemHovered())
                            {
                                ImGui.BeginTooltip();
                                ImGui.Text("Right Motor Strength in %%");
                                ImGui.EndTooltip();
                            }
                            ImGui.TreePop();
                            //ImGui.EndChild();
                        }
                        ImGui.PopID();
                    }

                    //ImGui.EndGroup();
                    foreach (var i in toRemoveSteps)
                    {
                        pattern.Steps.RemoveAt(i);
                    }
                    
                    ImGui.TreePop();
                }
                ImGui.PopID();
            }

            foreach (var pattern in toRemovePatterns)
            {
                config.Patterns.Remove(pattern);
            }
            ImGui.End();
            if (!changed) return shouldDrawConfigUi;
            save(config);
            return shouldDrawConfigUi;
        }
    }
}
