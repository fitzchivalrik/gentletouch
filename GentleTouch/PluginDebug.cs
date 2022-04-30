using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using FFXIVClientStructs.FFXIV.Client.Game;
using GentleTouch.Triggers;
using ImGuiNET;

#if DEBUG
namespace GentleTouch
{
    public partial class GentleTouch
    {
        
        [PluginService]
        [RequiredVersion("1.0")]
        private static GameGui _gameGui { get; set; }
        private void DrawDebugUi()
        {
            //ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xFF000000);
            ImGui.SetNextWindowBgAlpha(1);
            if(!ImGui.Begin($"{PluginName} Debug")) { ImGui.End(); return;}
            
            
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
            
            ImGui.Text("Current Conditions:");
            //ImGui.Separator();
            var didAny = false;

            for (var i = 0; i < Condition.MaxConditionEntries; i++) {
                var typedCondition = (ConditionFlag) i;
                var cond = _condition[typedCondition];

                if (!cond) {
                    continue;
                }

                didAny = true;

                ImGui.Text($"ID: {i} Enum: {typedCondition}");
            }

            if (!didAny) {
                ImGui.Text("None. Talk to a shop NPC or visit a market board to find out more!!!!!!!");
            }
            
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

            unsafe
            {
                var cooldown = ActionManager.Instance()->GetRecastGroupDetail(
                    _cooldownGroup - 1);
                ImGui.Text($"Cooldown Elapsed: {cooldown->Elapsed}");
                ImGui.Text($"Cooldown Total: {cooldown->Total}");
                ImGui.Text($"IsCooldown: {cooldown->IsActive}");
                ImGui.Text($"ActionID: {cooldown->ActionID}");
            }

            ImGui.Separator();
            
            if (_objects[0] is not PlayerCharacter localPlayer)
            {
                return;
            }
            ImGui.Text($"CurrentEnumerator: {_currentEnumerator}");
            
            for(var i = 0; i < _objects.Length; i ++)
            {
                var gameObject = _objects[i];
                if  (gameObject is null) continue;
                if (gameObject.ObjectKind != ObjectKind.EventObj) continue;
                //if (!_aetherCurrentNameWhitelist.Contains(actor.Name)) continue;
                // IF set (!=0), its invisible
                var visible = Marshal.ReadByte(gameObject.Address, 0x105);
                var direction = localPlayer.Position.Z - gameObject.Position.Z > 3
                    ? "DOWN"
                    : "UP";
                var actualDistance = gameObject.YalmDistanceX != 0
                    ? gameObject.YalmDistanceX
                    : Math.Sqrt(Math.Pow(localPlayer.Position.X - gameObject.Position.X, 2)
                                + Math.Pow(localPlayer.Position.Y - gameObject.Position.Y, 2)
                                + Math.Pow(localPlayer.Position.Z - gameObject.Position.Z, 2));

                //TODO So long as its not fixed in Dalamud
                var gameObjectName = gameObject.Name.TextValue;
                ImGui.Text($"{gameObject.ObjectId}:{gameObjectName}" +
                           $" ({gameObject.Position.X},{gameObject.Position.Y},{gameObject.Position.Z})" +
                           $" ({gameObject.Rotation})" +
                           $" ({gameObject.YalmDistanceX},{gameObject.YalmDistanceZ})" +
                           $" {direction}" +
                           $" RenderMode {visible:X}" +
                           $" Dis {actualDistance}");

                if(!_config.SenseAetherCurrents) continue;
                if (!_gameGui.WorldToScreen(gameObject.Position, out var screenCoords)) continue;
                
                if (actualDistance > _config.MaxAetherCurrentSenseDistanceSquared)
                    continue;
                ImGui.PushID(i);
                ImGui.SetNextWindowPos(new Vector2(screenCoords.X, screenCoords.Y));
                ImGui.SetNextWindowBgAlpha(
                    (float)Math.Max(1 - (actualDistance / _config.MaxAetherCurrentSenseDistanceSquared), 0.2));
                if (ImGui.Begin("Actor" + i,
                    ImGuiWindowFlags.NoDecoration |
                    ImGuiWindowFlags.AlwaysAutoResize |
                    ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoMouseInputs |
                    ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav)) {
                    ImGui.Text(
                        $"{gameObject.Address.ToInt64():X}-{gameObject.ObjectId:X}[{i}] - {gameObject.ObjectKind} - {gameObjectName} - {actualDistance} RenderMode {visible:X}");
                    ImGui.End();
                }
                ImGui.PopID();
            }

            ImGui.End();
        }

    }
}
#endif