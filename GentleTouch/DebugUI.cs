using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState.Actors;
using GentleTouch.Triggers;
using ImGuiNET;

#if DEBUG
namespace GentleTouch
{
    public partial class GentleTouch
    {
        private void DrawDebugUi()
        {
            //ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xFF000000);
            ImGui.SetNextWindowBgAlpha(1);
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
            if (_highestPriorityTrigger is CooldownTrigger ct)
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
            ImGui.Text($"IsCooldown: {cooldown.IsActive}");
            ImGui.Text($"ActionID: {cooldown.ActionID}");
            ImGui.Separator();
            
            var localPlayer = _pluginInterface.ClientState.LocalPlayer;
            ImGui.Text($"CurrentEnumerator: {_currentEnumerator}");
            
            for(var i = 0; i < _pluginInterface.ClientState.Actors.Length; i ++)
            {
                var actor = _pluginInterface.ClientState.Actors[i];
                if  (actor is null) continue;
                if (actor.ObjectKind != ObjectKind.EventObj) continue;
                //if (!_aetherCurrentNameWhitelist.Contains(actor.Name)) continue;
                // IF set (!=0), its invisible
                var visible = Marshal.ReadByte(actor.Address, 0x105);
                var direction = _pluginInterface.ClientState.LocalPlayer.Position.Z - actor.Position.Z > 3
                    ? "DOWN"
                    : "UP";
                var actualDistance = actor.YalmDistanceX != 0
                    ? actor.YalmDistanceX
                    : Math.Sqrt(Math.Pow(localPlayer.Position.X - actor.Position.X, 2)
                                + Math.Pow(localPlayer.Position.Y - actor.Position.Y, 2)
                                + Math.Pow(localPlayer.Position.Z - actor.Position.Z, 2));

                //TODO So long as its not fixed in Dalamud
                var actorName = Encoding.UTF8.GetString(Encoding.Default.GetBytes(actor.Name));
                ImGui.Text($"{actor.ActorId}:{actorName}" +
                           $" ({actor.Position.X},{actor.Position.Y},{actor.Position.Z})" +
                           $" ({actor.Rotation})" +
                           $" ({actor.YalmDistanceX},{actor.YalmDistanceY})" +
                           $" {direction}" +
                           $" RenderMode {visible:X}" +
                           $" Dis {actualDistance}");

                if(!_config.SenseAetherCurrents) continue;
                if (!_pluginInterface.Framework.Gui.WorldToScreen(actor.Position, out var screenCoords)) continue;
                
                if (actualDistance > _config.MaxAetherCurrentSenseDistance)
                    continue;
                ImGui.PushID(i);
                ImGui.SetNextWindowPos(new Vector2(screenCoords.X, screenCoords.Y));
                ImGui.SetNextWindowBgAlpha(
                    (float)Math.Max(1 - (actualDistance / _config.MaxAetherCurrentSenseDistance), 0.2));
                if (ImGui.Begin("Actor" + i,
                    ImGuiWindowFlags.NoDecoration |
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoMouseInputs |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)) {
                    ImGui.Text(
                        $"{actor.Address.ToInt64():X}-{actor.ActorId:X}[{i}] - {actor.ObjectKind} - {actorName} - {actualDistance} RenderMode {visible:X}");
                    ImGui.End();
                }
                ImGui.PopID();
            }

            ImGui.End();
        }

    }
}
#endif