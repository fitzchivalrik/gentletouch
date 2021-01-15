﻿using System;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;

namespace GentleTouch
{
    public class PluginUI
    {
        public bool IsVisible = true;
        private Configuration config;
        private GentleTouchPlugin gentle;
        private int leftMotorSpeed = 0;
        private int rightMotorSpeed = 0;

        public PluginUI(Configuration config, GentleTouchPlugin gentle)
        {
            this.config = config;
            this.gentle = gentle;
        }

        public unsafe void Draw()
        {
            var plugin = new {Name = "test"};
        
            if (!IsVisible)
                return;
            
            
            var drawConfig = true;
            var scale = ImGui.GetIO().FontGlobalScale;
            var windowFlags = ImGuiWindowFlags.NoCollapse;
            ImGui.SetNextWindowSizeConstraints(new Vector2(350 * scale, 200 * scale), new Vector2(600 * scale, 800 * scale));
            ImGui.Begin($"{plugin.Name} Config", ref IsVisible, windowFlags);
            
            ImGui.PushStyleColor(ImGuiCol.Button, 0xFF5E5BFF);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xFF5E5BAA);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xFF5E5BDD);
            //var c = ImGui.GetCursorPos();
            //ImGui.SetCursorPosX(ImGui.GetWindowContentRegionWidth() - ImGui.CalcTextSize("Support on Ko-fi").X);
            if (ImGui.SmallButton("BRBRBR")) {
                PluginLog.Information("BRBR?");
                PluginLog.Log($"LeftMotorSpeed: {leftMotorSpeed}, RightMotorSpeed: {rightMotorSpeed}");
                PluginLog.Log($"InputStruct  {new IntPtr(this.gentle.inputst)}");
                PluginLog.Log($"Ctor RETURN  {new IntPtr(this.gentle.inputstCtor)}");
                PluginLog.Log($"Ctor *RETURN {new IntPtr(this.gentle.inputStCtorDe)}");
                this.gentle.brbr(rightMotorSpeed, leftMotorSpeed);
            }
            if (ImGui.SmallButton("BRBRBR DIRECT")) {
                PluginLog.Information("BRBR DIRECT?");
                PluginLog.Log($"LeftMotorSpeed: {leftMotorSpeed}, RightMotorSpeed: {rightMotorSpeed}");
                this.gentle.brbrDirect(rightMotorSpeed, leftMotorSpeed);
            }
            if (ImGui.Button("BRBR STOP!")) {
                PluginLog.Information("NO BRBR!");
                this.gentle.brbr(0, 0);
            }
            ImGui.PopStyleColor(3);

            
            ImGui.Text($"InputStruct {new IntPtr(this.gentle.inputst).ToString("x8")}");
            if (this.gentle.inputst != null)
            {
                var brbrPtr = new IntPtr(this.gentle.inputst);
                var active_Pad_Number = *((int*) (brbrPtr + 876).ToPointer());
                ImGui.Text($"{nameof(active_Pad_Number)}: {active_Pad_Number}");
                var pad_array_ptr = *(long*)(brbrPtr + 96).ToPointer();
                ImGui.Text($"{nameof(pad_array_ptr)}: {pad_array_ptr}, as Pointer {new IntPtr(pad_array_ptr).ToString("x8")}");
                var cur_Pad_Ptr = new IntPtr(pad_array_ptr) + 1912 * active_Pad_Number;
                ImGui.Text($"{nameof(cur_Pad_Ptr)}: {cur_Pad_Ptr.ToString("x8")}");
                var xInputCheck = *(byte*) (cur_Pad_Ptr + 1);
                ImGui.Text($"{nameof(xInputCheck)} as byte: {xInputCheck}");
                var cur_Pad_xInput_Index = *(uint*) (cur_Pad_Ptr + 40);
                ImGui.Text($"{nameof(cur_Pad_xInput_Index)} as uint: {cur_Pad_xInput_Index}");
            }

            ImGui.Text($"Ctor RETURN  {new IntPtr(this.gentle.inputstCtor).ToString("x8")}");
            ImGui.Text($"Ctor *RETURN {new IntPtr(this.gentle.inputStCtorDe).ToString("x8")}");
            ImGui.Separator();
            ImGui.InputInt("RightMotorSpeed", ref rightMotorSpeed);
            ImGui.InputInt("LeftMotorSpeed", ref leftMotorSpeed);
            ImGui.Separator();
            
            
            
            var a = false;
            ImGui.PushStyleColor(ImGuiCol.FrameBg, 0x0);
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, 0x0);
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, 0x0);
            ImGui.Checkbox("###notARealCheckbox", ref a);
            ImGui.PopStyleColor(3);
            ImGui.SameLine();
            if (ImGui.TreeNode("General Options")) {
                if (ImGui.Checkbox("Show Experimental Tweaks.", ref this.config.ShowExperimentalTweaks))
                {
                    PluginLog.Log("Ticked Experimental");
                    this.config.Save();
                };
                if (ImGui.Checkbox("Hide Ko-fi link.", ref this.config.HideKofi))
                {
                    PluginLog.Log("Ticked Kofi");
                    this.config.Save();
                };
                ImGui.TreePop();
            }

            ImGui.End();
            
        }
    }
}
