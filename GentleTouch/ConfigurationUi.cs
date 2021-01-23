using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Configuration;
using Dalamud.Plugin;
using GentleTouch.Caraxi;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace GentleTouch
{
    internal static class ConfigurationUi
    {
        internal static bool DrawConfigUi(Configuration config, DalamudPluginInterface pi,
            Action<IPluginConfiguration> save, IReadOnlyCollection<ClassJob> jobs,
            IReadOnlyCollection<FFXIVAction> allActions)
        {
            var shouldDrawConfigUi = true;
            var changed = false;
            var scale = ImGui.GetIO().FontGlobalScale;
            // TODO (Chiv) Check if size fits
            ImGui.SetWindowSize(new Vector2(350 *scale, 200 *scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale),
                new Vector2(float.MaxValue, float.MaxValue));
            ImGui.Begin($"{Constant.PluginName} Configuration", ref shouldDrawConfigUi, ImGuiWindowFlags.NoCollapse);
            if (config.OptForNoUsage)
            {
                ImGui.Text("You choose to not acknowledge the risks and therefore cannot");
                ImGui.Text("use this plugin. Please remove it in the plugin installer.");
                ImGui.End();
                return shouldDrawConfigUi;
            }
            changed |= DrawRisksWarning(config);
            ImGui.BeginTabBar("ConfigurationTabs", ImGuiTabBarFlags.NoTooltip);
            changed |= DrawGeneralTab(config);
            changed |= DrawPatternTab(config,scale);
            changed |= DrawTriggerTab(config, pi, scale, jobs, allActions);
            ImGui.EndTabBar();
            ImGui.End();
            if (!changed) return shouldDrawConfigUi;
            save(config);
            return shouldDrawConfigUi;
        }

        private static bool DrawRisksWarning(Configuration config)
        {
            if(!config.RisksAcknowledged) ImGui.OpenPopup("Risks");
            ImGui.SetNextWindowSize(new Vector2(750, 225), ImGuiCond.Always);
            if(!ImGui.BeginPopupModal("Risks")) return false;
            ImGui.Text("This plugin allows you to directly use the motors of your controller.");
            ImGui.Text("Irresponsible usage has a probability of permanent hardware damage.");
            ImGui.Text("The author cannot be held liable for any damage caused by e.g. vibration overuse.");
            ImGui.Text("Before using this plugin you have to acknowledge the risks and that you are sole responsible for any damage which might occur.");
            ImGui.Text("You can cancel and remove the plugin if you do not consent.");
            ImGui.Text("Responsible usage should come with no risks.");
            ImGui.Spacing();
            var changed = false;
            if (ImGui.Button("I hereby acknowledge the risks and that the author cannot be held liable for damage due to misuse"))
            {
                config.RisksAcknowledged = true;
                changed = true;
                ImGui.CloseCurrentPopup();
            }

            if (ImGui.Button("Cancel"))
            {
                config.OptForNoUsage = true;
                changed = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
            return changed;
        }

        private static int _currentSelectedAction = 0;
        private static int _currentSelectedJob = 0;
        private static int _currentSelectedPattern = 0;

        private static bool DrawTriggerTab(Configuration config, DalamudPluginInterface pi, float scale,
            IReadOnlyCollection<ClassJob> jobs, IReadOnlyCollection<FFXIVAction> allActions)
        {
            if (!ImGui.BeginTabItem("Triggers")) return false;
            var changed = false;
            if (ImGui.TreeNode("Cooldown Triggers (work only in combat)"))
            {
                if(ImGui.Button("Add##Trigger"))
                {
                    var lastTrigger = config.CooldownTriggers.Last();
                    config.CooldownTriggers.Add(
                        new VibrationCooldownTrigger(
                            0, "GCD", 0, 58, lastTrigger.Priority+1,config.Patterns[0]));
                    changed = true;
                }
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text("Ordered by priority. Drag and Drop on ':::' to swap");
                int[] toSwap = {0, 0};
                var toRemoveTrigger = new List<VibrationCooldownTrigger>();
                foreach (var trigger in config.CooldownTriggers)
                {
                    ImGui.PushID(trigger.Priority);
                    
                    //ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                    ImGui.Button(":::");
                    if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoHoldToOpenOthers | ImGuiDragDropFlags.SourceNoPreviewTooltip))
                    {
                        unsafe
                        {
                            var prio = trigger.Priority;
                            var ptr = new IntPtr(&prio);
                            // TODO (chiv) const string
                            ImGui.SetDragDropPayload("PRIORITY_PAYLOAD",ptr , sizeof(int));
                        }
                        //ImGui.SameLine();
                        #if DEBUG
                        //ImGui.Text($"Dragging {trigger.ActionName} with prio {trigger.Priority}");
                        #else
                        //ImGui.Text($"Dragging {trigger.ActionName}");
                        #endif
                        ImGui.EndDragDropSource();
                    }
                    if (ImGui.BeginDragDropTarget())
                    {
                        var imGuiPayloadPtr = ImGui.AcceptDragDropPayload("PRIORITY_PAYLOAD", ImGuiDragDropFlags.AcceptBeforeDelivery | ImGuiDragDropFlags.AcceptNoPreviewTooltip | ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
                        unsafe
                        {
                            if (imGuiPayloadPtr.NativePtr is not null)
                            {
                                var prio = *(int*)imGuiPayloadPtr.Data;
                                toSwap[0] = prio;
                                toSwap[1] = trigger.Priority;
                            }
                        }
                        //ImGui.SameLine();
                        #if DEBUG
                        //ImGui.Text($"Swap {trigger.ActionName} with prio {trigger.Priority}");
                        #else
                        //ImGui.Text($"Swap with {trigger.ActionName}");
                        #endif
                        ImGui.EndDragDropTarget();
                    }
                    //ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.Text($"{trigger.Priority+1}");                    
                    #if DEBUG
                    ImGui.SameLine();
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"A:{trigger.ActionId} J:{trigger.JobId}");
                    #endif
                    ImGui.SetNextItemWidth(100);
                    ImGui.SameLine();
                    changed |= DrawJobCombo(jobs, trigger);
                    ImGui.PushItemWidth(125 * scale);
                    ImGui.SameLine();
                    changed |= DrawActionCombo(pi, allActions, config, trigger);
                    ImGui.SameLine();
                    changed |= DrawPatternCombo(config, trigger);
                    ImGui.PopItemWidth();
                    ImGui.SameLine();
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
                            toRemoveTrigger.Add(trigger);
                            changed = true;
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }
                    ImGui.PopID();
                }

                if (toSwap[0] != toSwap[1])
                {
                    var t = config.CooldownTriggers[toSwap[0]];
                    config.CooldownTriggers[toSwap[0]] = config.CooldownTriggers[toSwap[1]];
                    config.CooldownTriggers[toSwap[1]] = t;
                    config.CooldownTriggers[toSwap[0]].Priority = toSwap[0];
                    config.CooldownTriggers[toSwap[1]].Priority = toSwap[1];
                    toSwap[0] = toSwap[1] = 0;
                    changed = true;
                }

                foreach (var trigger in toRemoveTrigger)
                {
                    config.CooldownTriggers.Remove(trigger);
                }

                if(toRemoveTrigger.Count > 0)
                    for (var i = 0; i < config.CooldownTriggers.Count; i++)
                    {
                        config.CooldownTriggers[i].Priority = i;
                    }
                ImGui.Spacing();
                ImGui.TreePop();
            }
            ImGui.EndTabItem();
            return changed;
        }

        private static bool DrawPatternCombo(Configuration config, VibrationCooldownTrigger trigger)
        {
            if (!ImGui.BeginCombo($"##Pattern{trigger.Priority}", trigger.Pattern.Name)) return false;
            var changed = false;
            foreach (var p in config.Patterns)
            {
                var isSelected = p.Guid == trigger.PatternGuid;
                if (ImGui.Selectable(p.Name, isSelected))
                {
                    trigger.Pattern = p;
                    trigger.PatternGuid = p.Guid;
                    changed = true;
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
            return changed;
        }

        private static bool DrawActionCombo(DalamudPluginInterface pi,IReadOnlyCollection<FFXIVAction> allActions, Configuration config, VibrationCooldownTrigger trigger)
        {
            var changed = false;
            var actionsCollection = trigger.JobId == 0 
                ? allActions.Where(a => a.RowId == 0) 
                : allActions.Where(a => a.RowId != 0 && a.ClassJobCategory.Value.HasClass((uint) trigger.JobId));
            var actions = actionsCollection as FFXIVAction[] ?? actionsCollection.ToArray();
            if (!actions.Select(a => a.RowId).Contains((uint) trigger.ActionId))
            {
                trigger.ActionId = (int)actions[0].RowId;
                trigger.ActionName = actions[0].Name;
                trigger.ActionCooldownGroup = actions[0].CooldownGroup;
                changed = true;
            }
            if (!ImGui.BeginCombo($"##Action{trigger.Priority}", trigger.ActionName)) return changed;
            {
                foreach (var a in actions)
                {
                    var isSelected = a.RowId == trigger.ActionId;
                    if (ImGui.Selectable(a.Name, isSelected))
                    {
                        trigger.ActionId = (int)a.RowId;
                        trigger.ActionName = a.Name;
                        trigger.ActionCooldownGroup = a.CooldownGroup;
                        changed = true;
                    }

                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }

                ImGui.EndCombo();
            }

            return changed;
        }

        private static bool DrawJobCombo(IReadOnlyCollection<ClassJob> jobs, VibrationCooldownTrigger trigger)
        {
            if (!ImGui.BeginCombo($"##Jobs{trigger.Priority}", jobs.Single(j => j.RowId == trigger.JobId).NameEnglish)) return false;
            var changed = false;
            foreach (var job in jobs)
            {
                var isSelected = job.RowId == trigger.JobId;
                if (ImGui.Selectable(job.NameEnglish, isSelected))
                {
                    trigger.JobId = (int)job.RowId;
                    changed = true;
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
            ImGui.EndCombo();
            return changed;
        }

        private static bool DrawPatternTab(Configuration config, float scale)
        {
            if (!ImGui.BeginTabItem("Pattern")) return false;
            var changed = false;
            if (ImGui.Button("Add"))
            {
                config.Patterns.Add(new VibrationPattern());
                changed = true;
            }

            var toRemovePatterns = new List<VibrationPattern>();
            foreach (var pattern in config.Patterns)
            {
                ImGui.PushID(pattern.Guid.GetHashCode());
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
                        pattern.Steps.Add(new VibrationPattern.Step(0, 0));
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
            ImGui.EndTabItem();
            return changed;
        }

        private static bool DrawGeneralTab(Configuration config)
        {
            if (!ImGui.BeginTabItem("General")) return false;
            var changed = ImGui.Checkbox(nameof(config.ShouldVibrateDuringPvP), ref config.ShouldVibrateDuringPvP);
            changed |= ImGui.Checkbox(nameof(config.ShouldVibrateDuringCasting), ref config.ShouldVibrateDuringCasting);
            changed |= ImGui.Checkbox(nameof(config.OnlyVibrateWithDrawnWeapon), ref config.OnlyVibrateWithDrawnWeapon);
            ImGui.EndTabItem();
            return changed;
        }
    }
}
