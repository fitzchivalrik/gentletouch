# 5.4 Offsets for maybe controller struct(s)

        
```C#    
//inputst = pointer to maybeControllerStruct -> this of FFXIV_SetState and MaybePoll 
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

        
ImGui.Text($"Marshal Calculations");

var brbrPtr = new Inttr(this.gentle.inputst);
var pressed_button_bitflag = Marshal.ReadInt16(brbrPtr,0x88);
var active_Pad_Number = Marshal.ReadInt32(brbrPtr, 876);
ImGui.Text($"{nameof(active_Pad_Number)}: {active_Pad_Number}");
var pad_array_ptr = Marshal.ReadIntPtr(brbrPtr, 96);
ImGui.Text($"{nameof(pad_array_ptr)}: {pad_array_ptr.ToInt64()}, as Pointer {pad_array_ptr.ToString("x8")}");
var cur_Pad_Ptr = pad_array_ptr + 1912 * active_Pad_Number;
ImGui.Text($"{nameof(cur_Pad_Ptr)}: {cur_Pad_Ptr.ToString("x8")}");
var xInputCheck = Marshal.ReadByte(cur_Pad_Ptr, 1);
ImGui.Text($"{nameof(xInputCheck)} as byte: {xInputCheck}");
var cur_Pad_xInput_Index = Marshal.ReadInt16(cur_Pad_Ptr, 40);
var cur_Pad_xInput_Index_byte = Marshal.ReadByte(cur_Pad_Ptr, 40);
ImGui.Text($"{nameof(cur_Pad_xInput_Index)} as short: {cur_Pad_xInput_Index}");
ImGui.Text($"{nameof(cur_Pad_xInput_Index_byte)} as byte: {cur_Pad_xInput_Index_byte}");
```