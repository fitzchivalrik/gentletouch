using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Configuration;
using Dalamud.Interface;
using Dalamud.Plugin;
using GentleTouch.Interop;
using GentleTouch.Triggers;
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
            IReadOnlyCollection<FFXIVAction> allActions, ref IEnumerator<VibrationPattern.Step?>? patternEnumerator)
        {
            var shouldDrawConfigUi = true;
            var changed = false;
            var scale = ImGui.GetIO().FontGlobalScale;
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowSize(new Vector2(575 * scale, 400 * scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale),
                new Vector2(float.MaxValue, float.MaxValue));
            if (!ImGui.Begin($"{GentleTouch.PluginName} Configuration", ref shouldDrawConfigUi,
                ImGuiWindowFlags.NoCollapse))
            {
                ImGui.End();
                return shouldDrawConfigUi;
            }

            if (config.OnboardingStep != Onboarding.Done)
            {
                changed |= DrawRisksWarning(config, ref shouldDrawConfigUi, scale);
                changed |= DrawOnboarding(config, jobs, allActions, scale);
            }

            ImGui.BeginTabBar("ConfigurationTabs", ImGuiTabBarFlags.NoTooltip);
            changed |= DrawGeneralTab(config, scale);
            changed |= DrawPatternTab(config, scale, ref patternEnumerator);
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

        private static bool DrawOnboarding(Configuration config, IEnumerable<ClassJob> jobs,
            IEnumerable<FFXIVAction> allActions, float scale)
        {
            var contentSize = ImGuiHelpers.MainViewport.Size;
            var modalSize = new Vector2(300 * scale, 100 * scale);
            var modalPosition = new Vector2(contentSize.X / 2 - modalSize.X / 2, contentSize.Y / 2 - modalSize.Y / 2);

            if (config.OnboardingStep == Onboarding.AskAboutExamplePatterns)
                ImGui.OpenPopup("Create Example Patterns");
            var changed = false;
            ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
            if (ImGui.BeginPopupModal("Create Example Patterns"))
            {
                ImGui.Text($"No vibration patterns found." +
                           $"\nCreate some example patterns?");

                if (ImGui.Button("No##ExamplePatterns"))
                {
                    config.OnboardingStep = Onboarding.Done;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button(
                    "Yes##ExamplePatterns"))
                {
                    #region Example Patterns

                    var bothStrong = new VibrationPattern
                    {
                        Steps = new[]
                        {
                            new VibrationPattern.Step(100, 100, 300),
                        },
                        Cycles = 1,
                        Infinite = false,
                        Name = "Both Strong"
                    };
                    var lightPulse = new VibrationPattern
                    {
                        Steps = new[]
                        {
                            new VibrationPattern.Step(25, 25, 300),
                            new VibrationPattern.Step(0, 0, 400)
                        },
                        Infinite = true,
                        Name = "Light pulse"
                    };
                    var leftStrong = new VibrationPattern
                    {
                        Steps = new[]
                        {
                            new VibrationPattern.Step(100, 0, 300),
                        },
                        Infinite = false,
                        Cycles = 1,
                        Name = "Left Strong"
                    };
                    var rightStrong = new VibrationPattern
                    {
                        Steps = new[]
                        {
                            new VibrationPattern.Step(0, 100, 300),
                        },
                        Infinite = false,
                        Cycles = 1,
                        Name = "Right Strong"
                    };
                    var simpleRhythmic = new VibrationPattern
                    {
                        Steps = new[]
                        {
                            new VibrationPattern.Step(75, 75, 200),
                            new VibrationPattern.Step(0, 0, 200),
                        },
                        Infinite = false,
                        Cycles = 3,
                        Name = "Simple Rhythmic"
                    };
                    config.Patterns.Add(lightPulse);
                    config.Patterns.Add(bothStrong);
                    config.Patterns.Add(leftStrong);
                    config.Patterns.Add(rightStrong);
                    config.Patterns.Add(simpleRhythmic);

                    #endregion

                    config.OnboardingStep = Onboarding.AskAboutExampleCooldownTriggers;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (config.OnboardingStep == Onboarding.AskAboutExampleCooldownTriggers)
                ImGui.OpenPopup("Create Example Cooldown Triggers");
            ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
            if (ImGui.BeginPopupModal("Create Example Cooldown Triggers"))
            {
                ImGui.Text($"No cooldown triggers found." +
                           $"\nCreate some example triggers (Ninja and Paladin)?");

                if (ImGui.Button("No##ExampleTriggers"))
                {
                    config.OnboardingStep = Onboarding.Done;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button(
                    "Yes##ExampleTriggers"))
                {
                    #region Example Triggers

                    config.CooldownTriggers.Add(new CooldownTrigger(
                        30, "Dream Within a Dream", 3566, 16, 0, config.Patterns[1]));
                    config.CooldownTriggers.Add(new CooldownTrigger(
                        30, "Shadow Fang", 2257, 10, 1, config.Patterns[2]));
                    config.CooldownTriggers.Add(new CooldownTrigger(
                        30, "Mug", 2248, 18, 2, config.Patterns[3]));
                    config.CooldownTriggers.Add(new CooldownTrigger(
                        19, "Fight or Flight", 20, 14, 3, config.Patterns[1]));

                    #endregion

                    config.OnboardingStep = Onboarding.AskAboutGCD;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                    ImGui.OpenPopup("Create GCD Cooldown Triggers");
                }

                ImGui.EndPopup();
            }

            if (config.OnboardingStep == Onboarding.AskAboutGCD)
                ImGui.OpenPopup("Create GCD Cooldown Triggers");
            ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
            if (ImGui.BeginPopupModal("Create GCD Cooldown Triggers"))
            {
                ImGui.Text($"No GCD cooldown trigger found." +
                           $"\nCreate a GCD trigger for each job?");

                if (ImGui.Button("No##ExampleGCD"))
                {
                    config.OnboardingStep = Onboarding.Done;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button(
                    "Yes##ExampleGCD"))
                {
                    var gcdActionsCollection =
                        allActions.Where(a => a.CooldownGroup == CooldownTrigger.GCDCooldownGroup);
                    var gcdActions = gcdActionsCollection as FFXIVAction[] ?? gcdActionsCollection.ToArray();
                    foreach (var job in jobs)
                    {
                        var action = gcdActions.First(a => a.ClassJobCategory.Value.HasClass(job.RowId));
                        var lastTrigger = config.CooldownTriggers.LastOrDefault();
                        config.CooldownTriggers.Add(
                            new CooldownTrigger(
                                job.RowId,
                                action.Name,
                                action.RowId,
                                action.CooldownGroup,
                                lastTrigger?.Priority + 1 ?? 0,
                                config.Patterns[1]
                            ));
                    }

                    config.OnboardingStep = Onboarding.Done;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            return changed;
        }

        private static bool DrawRisksWarning(Configuration config, ref bool shouldDrawConfigUi, float scale)
        {
            if (config.OnboardingStep == Onboarding.TellAboutRisk) ImGui.OpenPopup("Warning");
            var contentSize = ImGuiHelpers.MainViewport.Size;
            var modalSize = new Vector2(500 * scale, 215 * scale);
            var modalPosition = new Vector2(contentSize.X / 2 - modalSize.X / 2, contentSize.Y / 2 - modalSize.Y / 2);
            ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
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
                config.OnboardingStep = Onboarding.AskAboutExamplePatterns;
                changed = true;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
            return changed;
        }

        private static bool DrawTriggerTab(Configuration config, DalamudPluginInterface pi, float scale,
            IEnumerable<ClassJob> jobs, IReadOnlyCollection<FFXIVAction> allActions)
        {
            if (!ImGui.BeginTabItem("Cooldown Triggers")) return false;
            var changed = false;
            changed |= DrawCooldownTriggers(config, scale, jobs, allActions);
            ImGui.EndTabItem();
            return changed;
        }

        private static bool DrawCooldownTriggers(Configuration config, float scale, IEnumerable<ClassJob> jobs,
            IReadOnlyCollection<FFXIVAction> allActions)
        {
            var changed = false;
            const FontAwesomeIcon dragDropMarker = FontAwesomeIcon.Sort;
            //if (!ImGui.CollapsingHeader("Cooldown Triggers (work only in combat)", ImGuiTreeNodeFlags.DefaultOpen)) return changed;
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Only working in combat. Ordered by priority; to swap, Drag'n'Drop on");
            ImGui.SameLine();
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(dragDropMarker.ToIconString());
            ImGui.PopFont();
            if (ImGui.Button("Add new Cooldown Trigger"))
            {
                var lastTrigger = config.CooldownTriggers.LastOrDefault();
                var firstAction =
                    allActions.First(a => a.ClassJobCategory.Value.HasClass(_currentJobTabId));
                config.CooldownTriggers.Add(
                    new CooldownTrigger(
                        _currentJobTabId,
                        firstAction.Name,
                        firstAction.RowId,
                        firstAction.CooldownGroup,
                        lastTrigger?.Priority + 1 ?? 0,
                        config.Patterns.FirstOrDefault() ?? new VibrationPattern()
                    ));
                changed = true;
            }

            int[] toSwap = {0, 0};
            //TODO (Chiv) This can be a single item, can't it?
            var toRemoveTrigger = new List<CooldownTrigger>();

            const ImGuiTabBarFlags tabBarFlags = ImGuiTabBarFlags.Reorderable
                                                 | ImGuiTabBarFlags.TabListPopupButton
                                                 | ImGuiTabBarFlags.FittingPolicyScroll;
            if (ImGui.BeginTabBar("JobsTabs", tabBarFlags))
            {
                foreach (var job in jobs)
                {
                    changed |= DrawJobTabItem(config, scale, allActions, dragDropMarker, job, toSwap, toRemoveTrigger);
                }

                ImGui.EndTabBar();
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

            return changed;
        }

        private static uint _currentJobTabId = 0;

        private static bool DrawJobTabItem(Configuration config, float scale, IEnumerable<FFXIVAction> allActions,
            FontAwesomeIcon dragDropMarker, ClassJob job, IList<int> toSwap,
            ICollection<CooldownTrigger> toRemoveTrigger)
        {
            if (!ImGui.BeginTabItem(job.NameEnglish)) return false;
            var changed = false;
            ImGui.Indent();
            ImGui.Indent(28 * scale);
            ImGui.Text("Action");
            ImGui.SameLine(215 * scale);
            ImGui.Text("Pattern");
            ImGui.Unindent(27 * scale);
            _currentJobTabId = job.RowId;
            var triggerForJob =
                config.CooldownTriggers.Where(t => t.JobId == _currentJobTabId);
            var actionsCollection =
                allActions.Where(a => a.ClassJobCategory.Value.HasClass(job.RowId));
            var actions = actionsCollection as FFXIVAction[] ?? actionsCollection.ToArray();
            foreach (var trigger in triggerForJob)
            {
                ImGui.PushID(trigger.Priority);
                
                ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
                ImGui.PushFont(UiBuilder.IconFont);
                ImGui.Button(dragDropMarker.ToIconString());
                ImGui.PopFont();
                ImGui.PopStyleColor();
                if (!DrawDragDropTargetSources(trigger, toSwap))
                {
                    ImGui.SameLine();
#if DEBUG
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"{trigger.Priority + 1:00}");
                    ImGui.SameLine();
                    ImGui.Text($"J:{trigger.JobId:00} A:{trigger.ActionId:0000} ");
#endif
                    ImGui.PushItemWidth(150 * scale);
                    ImGui.SameLine();
                    changed |= DrawActionCombo(actions, trigger);
                    ImGui.SameLine();
                    changed |= DrawPatternCombo(config.Patterns, trigger);
                    ImGui.PopItemWidth();
                    ImGui.SameLine();
                    if (DrawDeleteButton(FontAwesomeIcon.TrashAlt,
                        new Vector2(23 * scale, 23 * scale), "Delete this trigger."))
                    {
                        toRemoveTrigger.Add(trigger);
                        changed = true;
                    }
                }

                ImGui.PopID();
            }

            ImGui.Unindent();
            ImGui.EndTabItem();
            return changed;
        }

        private static bool DrawDragDropTargetSources(CooldownTrigger trigger, IList<int> toSwap)
        {
            const string payloadIdentifier = "PRIORITY_PAYLOAD";
            if (ImGui.BeginDragDropSource(ImGuiDragDropFlags.SourceNoHoldToOpenOthers |
                                          ImGuiDragDropFlags.SourceNoPreviewTooltip))
            {
                unsafe
                {
                    var prio = trigger.Priority;
                    var ptr = new IntPtr(&prio);
                    ImGui.SetDragDropPayload(payloadIdentifier, ptr, sizeof(int));
                }

                ImGui.SameLine();
                ImGui.AlignTextToFramePadding();
#if DEBUG
                ImGui.Text($"{trigger.Priority + 1} Dragging {trigger.ActionName}.");
#else
                ImGui.Text($"Dragging {trigger.ActionName}.");
#endif
                ImGui.EndDragDropSource();
                return true;
            }

            if (!ImGui.BeginDragDropTarget()) return false;
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

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
#if DEBUG
            ImGui.Text($"{trigger.Priority + 1} Swap with {trigger.ActionName}");
#else
            ImGui.Text($"Swap with {trigger.ActionName}");
#endif
            ImGui.EndDragDropTarget();
            return true;
        }

        private static bool DrawPatternCombo(IEnumerable<VibrationPattern> patterns, CooldownTrigger trigger)
        {
            if (!ImGui.BeginCombo($"##Pattern{trigger.Priority}", trigger.Pattern.Name)) return false;
            var changed = false;
            foreach (var p in patterns)
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

        private static bool DrawActionCombo(IEnumerable<FFXIVAction> actions, CooldownTrigger trigger)
        {
            if (!ImGui.BeginCombo($"##Action{trigger.Priority}",
#if DEBUG
                    trigger.ActionCooldownGroup == CooldownTrigger.GCDCooldownGroup
                        ? $"{trigger.ActionName} (GCD)"
                        : trigger.ActionName
#else
                trigger.ActionCooldownGroup == CooldownTrigger.GCDCooldownGroup ? "GCD" : trigger.ActionName
#endif
                )
            ) return false;
            var changed = false;
            foreach (var a in actions)
            {
                var isSelected = a.RowId == trigger.ActionId;
                if (ImGui.Selectable(
#if DEBUG
                    a.CooldownGroup == CooldownTrigger.GCDCooldownGroup ? $"{a.Name} (GCD)" : a.Name,
#else
                    a.CooldownGroup == CooldownTrigger.GCDCooldownGroup ? "GCD" : a.Name,
#endif
                    isSelected))
                {
                    trigger.ActionId = a.RowId;
                    trigger.ActionName = a.Name;
                    trigger.ActionCooldownGroup = a.CooldownGroup;
                    changed = true;
                }

                if (isSelected) ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
            return changed;
        }

        private static bool DrawPatternTab(Configuration config, float scale, ref IEnumerator<VibrationPattern.Step?>? patternEnumerator)
        {
            if (!ImGui.BeginTabItem("Patterns")) { return false; }
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

            ImGui.Separator();
            //TODO (Chiv) Single Item suffice?
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
                    var p = new VibrationPattern()
                    {
                        Cycles = pattern.Infinite ? 4 : pattern.Cycles,
                        Infinite = false,
                        Steps = pattern.Steps,
                        Name = pattern.Name
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

            foreach (var pattern in toRemovePatterns) config.Patterns.Remove(pattern);
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
                if (DrawDeleteButton(FontAwesomeIcon.TrashAlt,
                    new Vector2(23 * scale, 23 * scale),
                    "Delete this Step."))
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
                changed = true;
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
            if (ImGui.SliderInt("##Left Slider", ref s.LeftMotorPercentage, 0, 100, "Left Motor %d %%"))
                changed = true;
            ImGui.SameLine();
            if (ImGui.SliderInt("##Right Slider", ref s.RightMotorPercentage, 0, 100, "Right Motor %d %%"))
                changed = true;
            ImGui.SameLine();
            if (ImGui.DragInt("##MS Drag", ref s.MillisecondsTillNextStep, 10, 0, 2000, "%d ms till next"))
                changed = true;
            ImGui.PopItemWidth();
            return changed;
        }

        private static bool DrawDeleteButton(FontAwesomeIcon buttonLabel, Vector2? buttonSize = null,
            string? tooltipText = null)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushFont(UiBuilder.IconFont);
            var isButtonPressed = buttonSize is null
                ? ImGui.Button(buttonLabel.ToIconString())
                : ImGui.Button(buttonLabel.ToIconString(), buttonSize.Value);
            ImGui.PopFont();
            ImGui.PopStyleColor();
            if (isButtonPressed) ImGui.OpenPopup("Sure?");
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
                ImGui.Text($"Gradually stronger vibration the closer you are to an Aether Current.");
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
}