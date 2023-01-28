using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Plugin;
using GentleTouch.Interop;
using GentleTouch.Triggers;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;

namespace GentleTouch.UI;

internal partial class Config
{
    private static bool DrawTriggerTab(
        Configuration               config
      , DalamudPluginInterface      pi
      , float                       scale
      , IEnumerable<ClassJob>       jobs
      , IReadOnlyCollection<Action> allActions
    )
    {
        if (!ImGui.BeginTabItem("Cooldown Triggers")) return false;
        var changed = false;
        changed |= DrawCooldownTriggers(config, scale, jobs, allActions);
        ImGui.EndTabItem();
        return changed;
    }

    private static bool DrawCooldownTriggers(Configuration config, float scale, IEnumerable<ClassJob> jobs, IReadOnlyCollection<Action> allActions)
    {
        var                   changed        = false;
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
                allActions.First(a => a.ClassJobCategory.Value.HasClass(s_currentJobTabId));
            config.CooldownTriggers.Add(
                new CooldownTrigger(
                    s_currentJobTabId,
                    firstAction.Name,
                    firstAction.RowId,
                    firstAction.CooldownGroup,
                    lastTrigger?.Priority + 1 ?? 0,
                    config.Patterns.FirstOrDefault() ?? new VibrationPattern()
                ));
            changed = true;
        }

        int[] toSwap          = { 0, 0 };
        var   toRemoveTrigger = new List<CooldownTrigger>();

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
            (config.CooldownTriggers[toSwap[0]], config.CooldownTriggers[toSwap[1]]) =
                (config.CooldownTriggers[toSwap[1]], config.CooldownTriggers[toSwap[0]]);
            config.CooldownTriggers[toSwap[0]].Priority = toSwap[0];
            config.CooldownTriggers[toSwap[1]].Priority = toSwap[1];
            toSwap[0]                                   = toSwap[1] = 0;

            changed = true;
        }

        foreach (var trigger in toRemoveTrigger)
        {
            config.CooldownTriggers.Remove(trigger);
        }

        if (toRemoveTrigger.Count > 0)
        {
            for (var i = 0; i < config.CooldownTriggers.Count; i++)
            {
                config.CooldownTriggers[i].Priority = i;
            }
        }

        ImGui.Spacing();

        return changed;
    }

    private static bool DrawJobTabItem(
        Configuration                config
      , float                        scale
      , IEnumerable<Action>          allActions
      , FontAwesomeIcon              dragDropMarker
      , ClassJob                     job
      , IList<int>                   toSwap
      , ICollection<CooldownTrigger> toRemoveTrigger
    )
    {
        if (!ImGui.BeginTabItem(job.NameEnglish)) return false;
        var changed = false;
        ImGui.Indent();
        ImGui.Indent(28 * scale);
        ImGui.Text("Action");
        ImGui.SameLine(215 * scale);
        ImGui.Text("Pattern");
        ImGui.Unindent(27 * scale);
        s_currentJobTabId = job.RowId;
        var triggerForJob =
            config.CooldownTriggers.Where(t => t.JobId == s_currentJobTabId);
        var actionsCollection =
            allActions.Where(a => a.ClassJobCategory.Value.HasClass(job.RowId));
        var actions = actionsCollection as Action[] ?? actionsCollection.ToArray();
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
                var ptr  = new nint(&prio);
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
                var prio = *(int*)imGuiPayloadPtr.Data;
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
                trigger.Pattern     = p;
                trigger.PatternGuid = p.Guid;
                changed             = true;
            }

            if (isSelected) ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
        return changed;
    }

    private static bool DrawActionCombo(IEnumerable<Action> actions, CooldownTrigger trigger)
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
           )
        {
            return false;
        }

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
                trigger.ActionId            = a.RowId;
                trigger.ActionName          = a.Name;
                trigger.ActionCooldownGroup = a.CooldownGroup;
                changed                     = true;
            }

            if (isSelected) ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
        return changed;
    }
}