using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

#if DEBUG
namespace GentleTouch
{
    public partial class GentleTouch
    {
        private void DrawDebugUi()
        {
            if(!ImGui.Begin($"{PluginName} Debug")) return;
            ImGui.Text($"{_maybeControllerStruct.ToString("X12")}:{nameof(_maybeControllerStruct)}");
            ImGui.Text($"{nameof(ControllerPollDetour)} return Array (hex): ");
            foreach (var i in _lastReturnedFromPoll)
            {
                ImGui.SameLine();
                ImGui.Text($"{i:X}");
            }

            ImGui.Text(
                $"{Marshal.ReadInt32(_maybeControllerStruct, 0x88):X}:int (hex) at {nameof(_maybeControllerStruct)}+0x88");

            ImGui.Separator();
            ImGui.PushItemWidth(100);
            ImGui.InputInt("RightMotorSpeed", ref _rightMotorSpeed);
            ImGui.SameLine();
            ImGui.InputInt("LeftMotorSpeed", ref _leftMotorSpeed);
            ImGui.InputInt("Cooldown Group", ref _cooldownGroup);
            ImGui.SameLine();
            ImGui.InputInt("Controller Index", ref _dwControllerIndex);
            if (ImGui.Button("FFXIV SetState")) ControllerSetState((ushort) _leftMotorSpeed, (ushort) _rightMotorSpeed);

            ImGui.SameLine();
            if (ImGui.Button("XInput Wrapper SetSate"))
                ControllerSetState((ushort) _leftMotorSpeed, (ushort) _rightMotorSpeed, true);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 0, 0, 1));
            ImGui.SameLine();
            if (ImGui.Button("Stop Vibration"))
            {
                ControllerSetState(0, 0, true);
                ControllerSetState(0, 0);
            }

            ImGui.PopStyleColor();
            ImGui.PopItemWidth();
            ImGui.Separator();
            if (_highestPriorityTrigger is VibrationCooldownTrigger ct)
            {
                ImGui.Text("Current active Cooldown Trigger");
                ImGui.Text(
                    $"Priority:       {ct.Priority}" +
                    $"\nAction Name:  {ct.ActionName}" +
                    $"\nAction Id:      {ct.ActionId:00}" +
                    $"\nJob Id:         {ct.JobId:00}" +
                    $"\nPattern Name: {ct.Pattern.Name}");
                ImGui.Separator();
            }
            var cooldown = _getActionCooldownSlot(_actionManager, _cooldownGroup - 1);
            ImGui.Text($"Cooldown Elapsed: {cooldown.CooldownElapsed}");
            ImGui.Text($"Cooldown Total: {cooldown.CooldownTotal}");
            ImGui.Text($"IsCooldown: {cooldown.IsCooldown}");
            ImGui.Text($"ActionID: {cooldown.ActionID}");


            ImGui.End();
        }

    }
}
#endif