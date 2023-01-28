using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using GentleTouch.Interop;
using GentleTouch.Triggers;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace GentleTouch.UI;

internal static partial class Config
{
    private static bool DrawOnboarding(Configuration config, IEnumerable<ClassJob> jobs, IEnumerable<FFXIVAction> allActions, float scale)
    {
        var contentSize   = ImGuiHelpers.MainViewport.Size;
        var modalSize     = new Vector2(300 * scale, 100 * scale);
        var modalPosition = new Vector2(contentSize.X / 2 - modalSize.X / 2, contentSize.Y / 2 - modalSize.Y / 2);

        if (config.OnboardingStep == Onboarding.AskAboutExamplePatterns)
        {
            ImGui.OpenPopup("Create Example Patterns");
        }

        var changed = false;
        ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
        if (ImGui.BeginPopupModal("Create Example Patterns"))
        {
            ImGui.Text("No vibration patterns found." +
                       "\nCreate some example patterns?");

            if (ImGui.Button("No##ExamplePatterns"))
            {
                config.OnboardingStep = Onboarding.Done;
                changed               = true;
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
                        new VibrationPattern.Step(100, 100, 300)
                    }
                  , Cycles = 1, Infinite = false, Name = "Both Strong"
                };
                var lightPulse = new VibrationPattern
                {
                    Steps = new[]
                    {
                        new VibrationPattern.Step(25, 25, 300), new VibrationPattern.Step(0, 0, 400)
                    }
                  , Infinite = true, Name = "Light pulse"
                };
                var leftStrong = new VibrationPattern
                {
                    Steps = new[]
                    {
                        new VibrationPattern.Step(100, 0, 300)
                    }
                  , Infinite = false, Cycles = 1, Name = "Left Strong"
                };
                var rightStrong = new VibrationPattern
                {
                    Steps = new[]
                    {
                        new VibrationPattern.Step(0, 100, 300)
                    }
                  , Infinite = false, Cycles = 1, Name = "Right Strong"
                };
                var simpleRhythmic = new VibrationPattern
                {
                    Steps = new[]
                    {
                        new VibrationPattern.Step(75, 75, 200), new VibrationPattern.Step(0, 0, 200)
                    }
                  , Infinite = false, Cycles = 3, Name = "Simple Rhythmic"
                };
                config.Patterns.Add(lightPulse);
                config.Patterns.Add(bothStrong);
                config.Patterns.Add(leftStrong);
                config.Patterns.Add(rightStrong);
                config.Patterns.Add(simpleRhythmic);

                #endregion

                config.OnboardingStep = Onboarding.AskAboutExampleCooldownTriggers;
                changed               = true;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        if (config.OnboardingStep == Onboarding.AskAboutExampleCooldownTriggers)
        {
            ImGui.OpenPopup("Create Example Cooldown Triggers");
        }

        ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
        if (ImGui.BeginPopupModal("Create Example Cooldown Triggers"))
        {
            ImGui.Text("No cooldown triggers found." +
                       "\nCreate some example triggers (Ninja and Paladin)?");

            if (ImGui.Button("No##ExampleTriggers"))
            {
                config.OnboardingStep = Onboarding.Done;
                changed               = true;
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
                    30, "Mug", 2248, 18, 2, config.Patterns[3]));
                config.CooldownTriggers.Add(new CooldownTrigger(
                    19, "Fight or Flight", 20, 14, 3, config.Patterns[1]));

                #endregion

                config.OnboardingStep = Onboarding.AskAboutGCD;
                changed               = true;
                ImGui.CloseCurrentPopup();
                ImGui.OpenPopup("Create GCD Cooldown Triggers");
            }

            ImGui.EndPopup();
        }

        if (config.OnboardingStep == Onboarding.AskAboutGCD)
        {
            ImGui.OpenPopup("Create GCD Cooldown Triggers");
        }

        ImGui.SetNextWindowSize(modalSize, ImGuiCond.Always);
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(modalPosition, ImGuiCond.Always);
        if (ImGui.BeginPopupModal("Create GCD Cooldown Triggers"))
        {
            ImGui.Text("No GCD cooldown trigger found." +
                       "\nCreate a GCD trigger for each job?");

            if (ImGui.Button("No##ExampleGCD"))
            {
                config.OnboardingStep = Onboarding.Done;
                changed               = true;
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
                    var action      = gcdActions.First(a => a.ClassJobCategory.Value!.HasClass(job.RowId));
                    var lastTrigger = config.CooldownTriggers.LastOrDefault();
                    config.CooldownTriggers.Add(
                        new CooldownTrigger(
                            job.RowId,
                            action.Name,
                            action.RowId,
                            action.CooldownGroup,
                            lastTrigger?.Priority + 1 ?? 0,
                            config.Patterns[0]
                        ));
                }

                config.OnboardingStep = Onboarding.Done;
                changed               = true;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }

        return changed;
    }

    private static bool DrawRisksWarning(Configuration config, ref bool shouldDrawConfigUi, float scale)
    {
        if (config.OnboardingStep == Onboarding.TellAboutRisk) ImGui.OpenPopup("Warning");
        var contentSize   = ImGuiHelpers.MainViewport.Size;
        var modalSize     = new Vector2(500 * scale, 215 * scale);
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
            changed               = true;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
        return changed;
    }
}