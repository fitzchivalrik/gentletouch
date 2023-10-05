using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using GentleTouch.Triggers;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using FFXIVAction = Lumina.Excel.GeneratedSheets.Action;

namespace GentleTouch.UI;

internal static partial class Config
{
    private static uint s_currentJobTabId;

    internal static (bool, bool) DrawConfigUi(
        Configuration                            config
      , DalamudPluginInterface                   pi
      , IReadOnlyCollection<ClassJob>            jobs
      , IReadOnlyCollection<FFXIVAction>         allActions
      , ref IEnumerator<VibrationPattern.Step?>? patternEnumerator
    )
    {
        var shouldDrawConfigUi = true;
        var changed            = false;
        var scale              = ImGui.GetIO().FontGlobalScale;
        ImGuiHelpers.ForceNextWindowMainViewport();
        ImGui.SetNextWindowSize(new Vector2(575 * scale, 400 * scale), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale),
            new Vector2(float.MaxValue, float.MaxValue));
        if (!ImGui.Begin($"{GentleTouch.PluginName} Configuration", ref shouldDrawConfigUi,
                ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return (shouldDrawConfigUi, changed);
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
        changed |= DrawPlaystationTab(config, scale);
        ImGui.EndTabBar();
        ImGui.End();
        return (shouldDrawConfigUi, changed);
    }

    private static bool DrawDeleteButton(FontAwesomeIcon buttonLabel, Vector2? buttonSize = null, string? tooltipText = null)
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
}