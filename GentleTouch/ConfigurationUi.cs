using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Configuration;
using Dalamud.Plugin;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
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
            ImGui.SetNextWindowSize(new Vector2(575 * scale, 400 * scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale),
                new Vector2(float.MaxValue, float.MaxValue));
            if (!ImGui.Begin($"{GentleTouch.PluginName} Configuration", ref shouldDrawConfigUi,
                ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.HorizontalScrollbar))
                return shouldDrawConfigUi;

            changed |= DrawRisksWarning(config, ref shouldDrawConfigUi);
            ImGui.BeginTabBar("ConfigurationTabs", ImGuiTabBarFlags.NoTooltip);
            changed |= DrawGeneralTab(config);
            changed |= DrawPatternTab(config, scale);
            changed |= DrawTriggerTab(config, pi, scale, jobs, allActions);
            ImGui.EndTabBar();
            ImGui.End();
            if (!changed) return shouldDrawConfigUi;
            #if DEBUG
            PluginLog.Verbose("Config changed, saving...");
            #endif
            save(config);
            return shouldDrawConfigUi;
        }

        private static bool DrawRisksWarning(Configuration config, ref bool shouldDrawConfigUi)
        {
            if (!config.RisksAcknowledged) ImGui.OpenPopup("Warning");
            ImGui.SetNextWindowSize(new Vector2(500, 215), ImGuiCond.Always);
            if (!ImGui.BeginPopupModal("Warning")) return false;
            ImGui.Text("This plugin allows you to directly use the motors of your controller.");
            ImGui.Text("Irresponsible usage has a probability of permanent hardware damage.");
            ImGui.Text("The author cannot be held liable for any damage caused by e.g. vibration overuse.");
            ImGui.TextWrapped(
                "Before using this plugin you have to acknowledge the risks and that you are sole responsible for any damage which might occur.");
            ImGui.Text("You can cancel and remove the plugin if you do not consent.");
            ImGui.Text("Responsible usage should come with no risks.");
            ImGui.Spacing();
            var changed = false;
            if (ImGui.Button("Cancel##Risks"))
            {
                shouldDrawConfigUi = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button(
                "I hereby acknowledge the risks"))
            {
                config.RisksAcknowledged = true;
                changed = true;
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
            return changed;
        }
        
        private static bool DrawTriggerTab(Configuration config, DalamudPluginInterface pi, float scale,
            IReadOnlyCollection<ClassJob> jobs, IReadOnlyCollection<FFXIVAction> allActions)
        {
            if (!ImGui.BeginTabItem("Triggers")) return false;
            const string dragDropMarker = "::";
            var changed = false;
            if (ImGui.TreeNodeEx("Cooldown Triggers (work only in combat)", ImGuiTreeNodeFlags.Framed))
            {
                if (ImGui.Button("Add new Cooldown Trigger"))
                {
                    var lastTrigger = config.CooldownTriggers.LastOrDefault();
                    config.CooldownTriggers.Add(
                        new VibrationCooldownTrigger(
                            0, "GCD", 0, 58, (lastTrigger?.Priority + 1) ?? 0, config.Patterns[0]));
                    changed = true;
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"Ordered by priority. Drag and Drop on '{dragDropMarker}' to swap");
                int[] toSwap = {0, 0};
                //TODO (Chiv) This can be a single item, can't it?
                var toRemoveTrigger = new List<VibrationCooldownTrigger>();
                foreach (var trigger in config.CooldownTriggers)
                {
                    ImGui.PushID(trigger.Priority);
                    if(!DrawDragDropTargetSources(trigger, toSwap, dragDropMarker))
                    {
                        //ImGui.SetCursorPos(pos);
                        //ImGui.AlignTextToFramePadding();
                        //ImGui.Text(":::");
                        ImGui.SameLine();
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text($"{trigger.Priority + 1}");
#if DEBUG
                        ImGui.SameLine();
                        ImGui.Text($"A:{trigger.ActionId} J:{trigger.JobId}");
#endif
                        ImGui.SetNextItemWidth(100);
                        ImGui.SameLine();
                        changed |= DrawJobCombo(jobs, trigger);
                        ImGui.PushItemWidth(125 * scale);
                        ImGui.SameLine();
                        changed |= DrawActionCombo(allActions, trigger);
                        ImGui.SameLine();
                        changed |= DrawPatternCombo(config, trigger);
                        ImGui.PopItemWidth();
                        ImGui.SameLine();
                        if (DrawDeleteButton("X", new Vector2(23 * scale, 23 * scale), "Delete this pattern."))
                        {
                            toRemoveTrigger.Add(trigger);
                            changed = true;
                        }
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

                foreach (var trigger in toRemoveTrigger) config.CooldownTriggers.Remove(trigger);

                if (toRemoveTrigger.Count > 0)
                    for (var i = 0; i < config.CooldownTriggers.Count; i++)
                        config.CooldownTriggers[i].Priority = i;
                ImGui.Spacing();
                ImGui.TreePop();
            }

            ImGui.EndTabItem();
            return changed;
        }

        private static bool DrawDragDropTargetSources(VibrationCooldownTrigger trigger, IList<int> toSwap, string dragDropMarker)
        {
            const string payloadIdentifier = "PRIORITY_PAYLOAD";
            //TODO (Chiv): Button style
            //ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.Button(dragDropMarker);
            //ImGui.PopStyleColor();
            // NOTE (chiv) Alternative with selectable with explicit size and cursor swapping
            //var pos = ImGui.GetCursorPos();
            //var selectableSize = ImGui.CalcTextSize(":::");
            //var _ = false;
            //ImGui.Selectable("##:::", ref _, ImGuiSelectableFlags.None, selectableSize);
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoHoldToOpenOthers |
                                          ImGuiDragDropFlags.SourceNoPreviewTooltip))
            {
                unsafe
                {
                    var prio = trigger.Priority;
                    var ptr = new IntPtr(&prio);
                    ImGui.SetDragDropPayload(payloadIdentifier, ptr, sizeof(int));
                }
                //ImGui.SetCursorPos(pos);
                //ImGui.AlignTextToFramePadding();
                //ImGui.Text(":::");
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{trigger.Priority + 1} Dragging {trigger.ActionName}.");
                ImGui.EndDragDropSource();
                return true;
            }

            if (!ImGui.BeginDragDropTarget()) return false;
            {
                var imGuiPayloadPtr = ImGui.AcceptDragDropPayload(payloadIdentifier,
                    ImGuiDragDropFlags.AcceptNoPreviewTooltip |
                    ImGuiDragDropFlags.AcceptNoDrawDefaultRect);
                unsafe
                {
                    if (imGuiPayloadPtr.NativePtr is not null)
                    {
                        var prio = *(int*) imGuiPayloadPtr.Data;
                        toSwap[0] = prio;
                        toSwap[1] = trigger.Priority;
                    }
                }
                //ImGui.SetCursorPos(pos);
                //ImGui.AlignTextToFramePadding();
                //ImGui.Text(":::");
                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"{trigger.Priority + 1} Swap with {trigger.ActionName}");
                ImGui.EndDragDropTarget();
                return true;
            }
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

                if (isSelected) ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
            return changed;
        }

        private static bool DrawActionCombo(IReadOnlyCollection<FFXIVAction> allActions, VibrationCooldownTrigger trigger)
        {
            var actionsCollection = trigger.JobId == 0
                ? allActions.Where(a => a.RowId == 0)
                : allActions.Where(a => a.RowId != 0 && a.ClassJobCategory.Value.HasClass((uint) trigger.JobId));
            var actions = actionsCollection as FFXIVAction[] ?? actionsCollection.ToArray();
            var changed = false;
            if (!actions.Select(a => a.RowId).Contains((uint) trigger.ActionId))
            {
                trigger.ActionId = (int) actions[0].RowId;
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
                        trigger.ActionId = (int) a.RowId;
                        trigger.ActionName = a.Name;
                        trigger.ActionCooldownGroup = a.CooldownGroup;
                        changed = true;
                    }

                    if (isSelected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
            return changed;
        }

        private static bool DrawJobCombo(IReadOnlyCollection<ClassJob> jobs, VibrationCooldownTrigger trigger)
        {
            if (!ImGui.BeginCombo($"##Jobs{trigger.Priority}",
                jobs.Single(j => j.RowId == trigger.JobId).NameEnglish)) return false;
            var changed = false;
            foreach (var job in jobs)
            {
                var isSelected = job.RowId == trigger.JobId;
                if (ImGui.Selectable(job.NameEnglish, isSelected))
                {
                    trigger.JobId = (int) job.RowId;
                    changed = true;
                }

                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
            return changed;
        }

        private static bool DrawPatternTab(Configuration config, float scale)
        {
            if (!ImGui.BeginTabItem("Patterns")) return false;
            var changed = false;
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
                        var json = Encoding.UTF8.GetString(base64Bytes);
                        var p = JsonConvert.DeserializeObject<VibrationPattern>(json);
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

            //TODO (Chiv) Single Item suffice?
            var toRemovePatterns = new List<VibrationPattern>();
            foreach (var pattern in config.Patterns)
            {
                ImGui.PushID(pattern.Guid.GetHashCode());
                if (DrawDeleteButton("Delete"))
                {
                    toRemovePatterns.Add(pattern);
                    changed = true;
                }

                ImGui.SameLine();
                var open = ImGui.TreeNodeEx($"{pattern.Name}###{pattern.Guid}", ImGuiTreeNodeFlags.AllowItemOverlap);
                ImGui.SameLine(225);
                if(ImGui.Button("Export to clipboard"))
                {
                    var jsonOutput = JsonConvert.SerializeObject(pattern);
                    if (jsonOutput is not null)
                    {
                        var prefixedBase64 = $"{importExportPrefix}{Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonOutput))}";
                        ImGui.SetClipboardText(prefixedBase64);
                    }
                }
                if (open)
                {
                    changed |= DrawPatternGenerals(pattern);
                    changed |= DrawPatternSteps(scale, pattern);
                }
                ImGui.PopID();
            }

            foreach (var pattern in toRemovePatterns) config.Patterns.Remove(pattern);
            ImGui.EndTabItem();
            return changed;
        }

        private static bool DrawPatternSteps(float scale, VibrationPattern pattern)
        {
            var changed = false;
            if (ImGui.Button("+##PatternStep", new Vector2(23*scale, 23*scale)))
            {
                pattern.Steps.Add(new VibrationPattern.Step(0, 0));
                changed = true;
            }
            // TODO  (Chiv) Single item suffice?
            var toRemoveSteps = new List<int>();
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Steps");
            // TODO (Chiv) Ability to hide steps needed/desired?
            //if (ImGui.TreeNodeEx($"Steps", ImGuiTreeNodeFlags.DefaultOpen))
            //{
                for (var i = 0; i < pattern.Steps.Count; i++)
                {
                    ImGui.PushID(i);
                    var s = pattern.Steps[i];
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"{i + 1}");
                    ImGui.SameLine();
                    changed |= DrawHorizontalStep(scale, s);
                    ImGui.SameLine();
                    if (DrawDeleteButton("X", new Vector2(23 * scale, 23 * scale), "Delete this Step."))
                    {
                        toRemoveSteps.Add(i);
                        changed = true;
                    }
                    //changed = DrawVerticalStep(scale, toRemoveSteps, i, s);
                    ImGui.PopID();
                }
                // ImGui.TreePop();
            //}
            foreach (var i in toRemoveSteps) pattern.Steps.RemoveAt(i);
            ImGui.TreePop();
            return changed;
        }

        private static bool DrawPatternGenerals(VibrationPattern pattern)
        {
            #if DEBUG
            ImGui.Text($"UUID: {pattern.Guid}");
            if (ImGui.IsItemClicked(1)) ImGui.SetClipboardText(pattern.Guid.ToString());
            #endif
            var changed = false;
            ImGui.SetNextItemWidth(175);
            var n = pattern.Name == "Nameless" ? "" : pattern.Name;
            if (ImGui.InputTextWithHint("##Pattern Name", "Name of Pattern", ref n, 20) && n.Trim() != "")
            {
                pattern.Name = n;
                changed = true;
            }

            ImGui.SameLine();
            if (ImGui.Checkbox("Infinite", ref pattern.Infinite)) changed = true;
            if (pattern.Infinite) return changed;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(75);
            if (ImGui.InputInt("Cycles", ref pattern.Cycles, 1)) changed = true;
            return changed;
        }

        private static bool DrawVerticalStep(float scale, List<int> toRemoveSteps, int i,
            VibrationPattern.Step s)
        {
            var changed = false;
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 4));
            if (ImGui.Button("X", new Vector2(23 * scale, 23 * scale))) ImGui.OpenPopup("Sure?2");

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
            var centisecondsTillNextStep = s.MillisecondsTillNextStep;
            if (ImGui.InputInt("##centi", ref centisecondsTillNextStep, 0))
            {
                s.MillisecondsTillNextStep = centisecondsTillNextStep * 10;
                changed = true;
            }

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

            return changed;
        }

        private static bool DrawHorizontalStep(float scale, VibrationPattern.Step s)
        {
            var changed = false;
            ImGui.PushItemWidth(150 * scale);
            if (ImGui.SliderInt("##Left Slider", ref s.LeftMotorPercentage, 0, 100, "Left Motor %d %%"))
                changed = true;
            ImGui.SameLine();
            if (ImGui.SliderInt("##Right Slider", ref s.RightMotorPercentage, 0, 100, "Right Motor %d %%"))
                changed = true;
            ImGui.SameLine();
            //ImGui.SetNextItemWidth(100 * scale);
            if (ImGui.DragInt("##MS Drag", ref s.MillisecondsTillNextStep, 10, 0, 2000, "%d ms till next"))
                changed = true;
            ImGui.PopItemWidth();
            return changed;
        }

        private static bool DrawDeleteButton(string buttonLabel, Vector2? buttonSize = null, string? tooltipText = null)
        {
            if (buttonSize is null ? ImGui.Button(buttonLabel) : ImGui.Button(buttonLabel, buttonSize.Value)) ImGui.OpenPopup("Sure?");
            if (tooltipText is not null && ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text(tooltipText);
                ImGui.EndTooltip();
            }
            if (!ImGui.BeginPopup("Sure?")) return false;
            var consent = false;
            ImGui.Text("Really delete?");
            if (ImGui.Button("Yes"))
            {
                consent = true;
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
            return consent;
        }
        
        private static bool DrawGeneralTab(Configuration config)
        {
            if (!ImGui.BeginTabItem("General")) return false;
            var changed = ImGui.Checkbox("Disable cooldown trigger while casting.", ref config.NoVibrationDuringCasting);
            changed |= ImGui.Checkbox("Disable cooldown trigger while weapon is sheathed.", ref config.NoVibrationWithSheathedWeapon);
            ImGui.EndTabItem();
            return changed;
        }
    }
}