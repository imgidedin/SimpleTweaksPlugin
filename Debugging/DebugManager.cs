﻿using Dalamud.Game.Chat.SeStringHandling;
using Dalamud.Game.Chat.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Internal.Libc;
using SimpleTweaksPlugin.Debugging;
using SimpleTweaksPlugin.Helper;

namespace SimpleTweaksPlugin {
    public partial class SimpleTweaksPluginConfig {
        public DebugConfig Debugging = new DebugConfig();

        public bool ShouldSerializeDebugging() {
            return DebugManager.Enabled;
        }
        
    }
}

namespace SimpleTweaksPlugin.Debugging {

    public partial class DebugConfig {
        public string SelectedPage = String.Empty;
    }

    public abstract class DebugHelper : IDisposable {
        public SimpleTweaksPlugin Plugin;
        public abstract void Draw();
        public abstract string Name { get; }

        public virtual void Dispose() {
            
        }

    }
    
    public class DebugManager : IDisposable {

        private static Dictionary<string, Action> debugPages = new Dictionary<string, Action>();

        private static float sidebarSize = 0;
        
        public static bool Enabled = true;

        public static void RegisterDebugPage(string key, Action action) {
            if (debugPages.ContainsKey(key)) {
                debugPages[key] = action;
            } else {
                debugPages.Add(key, action);
            }

            sidebarSize = 0;
        }

        public static void RemoveDebugPage(string key) {
            if (debugPages.ContainsKey(key)) {
                debugPages.Remove(key);
            }

            sidebarSize = 0;
        }

        private static SimpleTweaksPlugin _plugin;

        private static bool _setupDebugHelpers = false;

        private static readonly List<DebugHelper> DebugHelpers = new List<DebugHelper>();

        public static void SetPlugin(SimpleTweaksPlugin plugin) {
            _plugin = plugin;
        }

        public static void DrawDebugWindow(ref bool open) {
            if (_plugin == null) return;

            if (!_setupDebugHelpers) {
                _setupDebugHelpers = true;
                foreach (var t in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.IsSubclassOf(typeof(DebugHelper)) && !t.IsAbstract)) {
                    var debugger = (DebugHelper)Activator.CreateInstance(t);
                    debugger.Plugin = _plugin;
                    RegisterDebugPage(debugger.Name, debugger.Draw);
                    DebugHelpers.Add(debugger);
                }
            }

            if (sidebarSize < 150) {
                sidebarSize = 150;
                foreach (var k in debugPages.Keys) {
                    var s = ImGui.CalcTextSize(k).X + ImGui.GetStyle().FramePadding.X * 5 + ImGui.GetStyle().ScrollbarSize;
                    if (s > sidebarSize) {
                        sidebarSize = s;
                    }
                }
            }

            ImGui.SetNextWindowSize(new Vector2(500, 350) * ImGui.GetIO().FontGlobalScale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(350, 350) * ImGui.GetIO().FontGlobalScale, new Vector2(2000, 2000) * ImGui.GetIO().FontGlobalScale);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, 0xFF000000);
            if (ImGui.Begin($"SimpleTweaksPlugin - Debug", ref open)) {
                
                if (ImGui.BeginChild("###debugPages", new Vector2(sidebarSize, -1) * ImGui.GetIO().FontGlobalScale, true)) {

                    foreach (var k in debugPages.Keys) {

                        if (ImGui.Selectable($"{k}##debugPageOption", _plugin.PluginConfig.Debugging.SelectedPage == k)) {
                            _plugin.PluginConfig.Debugging.SelectedPage = k;
                            _plugin.PluginConfig.Save();
                        }
                        
                    }

                    ImGui.EndChild();
                }

                ImGui.SameLine();

                if (ImGui.BeginChild("###debugView", new Vector2(-1, -1), true, ImGuiWindowFlags.HorizontalScrollbar)){
                    if (string.IsNullOrEmpty(_plugin.PluginConfig.Debugging.SelectedPage) || !debugPages.ContainsKey(_plugin.PluginConfig.Debugging.SelectedPage)) {
                        ImGui.Text("Select Debug Page");
                    } else {
                        debugPages[_plugin.PluginConfig.Debugging.SelectedPage]();
                    }
                    ImGui.EndChild();
                }


                ImGui.End();
            }
            ImGui.PopStyleColor();
        }

        public void Dispose() {
            foreach (var debugger in DebugHelpers) {
                RemoveDebugPage(debugger.Name);
                debugger.Dispose();
            }
            DebugHelpers.Clear();
        }


        private static unsafe Vector2 GetNodePosition(AtkResNode* node) {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null) {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }
            return pos;
        }

        private static unsafe Vector2 GetNodeScale(AtkResNode* node) {
            if (node == null) return new Vector2(1, 1);
            var scale = new Vector2(node->ScaleX, node->ScaleY);
            while (node->ParentNode != null) {
                node = node->ParentNode;
                scale *= new Vector2(node->ScaleX, node->ScaleY);
            }
            return scale;
        }

        private static unsafe bool GetNodeVisible(AtkResNode* node) {
            if (node == null) return false;
            while (node != null) {
                if ((node->Flags & (short)NodeFlags.Visible) != (short)NodeFlags.Visible) return false;
                node = node->ParentNode;
            }
            return true;
        }
        
        public static unsafe void HighlightResNode(AtkResNode* node) {
            var position = GetNodePosition(node);
            var scale = GetNodeScale(node);
            var size = new Vector2(node->Width, node->Height) * scale;

            var nodeVisible = GetNodeVisible(node);
            ImGui.GetForegroundDrawList().AddRectFilled(position, position+size, (uint) (nodeVisible ? 0x5500FF00 : 0x550000FF));
            ImGui.GetForegroundDrawList().AddRect(position, position + size, nodeVisible ? 0xFF00FF00 : 0xFF0000FF);
            
        }

        public static void ClickToCopyText(string text, string textCopy = null) {
            textCopy ??= text;
            ImGui.Text($"{text}");
            if (ImGui.IsItemHovered()) {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (textCopy != text) ImGui.SetTooltip(textCopy);
            }
            if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"{textCopy}");
        }

        public static unsafe void SeStringToText(SeString seStr) {
            var pushColorCount = 0;

            ImGui.BeginGroup();
            foreach (var p in seStr.Payloads) {
                switch (p) {
                    case UIForegroundPayload c:
                        if (c.ColorKey == 0) {
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFFFFFF);
                            pushColorCount++;
                            break;
                        }
                        var r = (c.UIColor.UIForeground >> 0x18) & 0xFF;
                        var g = (c.UIColor.UIForeground >> 0x10) & 0xFF;
                        var b = (c.UIColor.UIForeground >> 0x08) & 0xFF;
                        var a = (c.UIColor.UIForeground >> 0x00) & 0xFF;

                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(r / 255f, g / 255f, b / 255f, 1));
                        pushColorCount++;
                        break;
                    case TextPayload t:
                        ImGui.Text($"{t.Text}");
                        ImGui.SameLine();
                        break;
                }
            }
            ImGui.EndGroup();
            if (pushColorCount > 0) ImGui.PopStyleColor(pushColorCount);
        }

        private static ulong beginModule = 0;
        private static ulong endModule = 0;
        
        private static unsafe void PrintOutValue(ulong addr, List<string> path, Type type, object value, MemberInfo member) {
            try {
                var valueParser = member.GetCustomAttribute(typeof(ValueParser));
                var fieldOffset = member.GetCustomAttribute(typeof(FieldOffsetAttribute));
                if (valueParser is ValueParser vp) {
                    vp.ImGuiPrint(type, value, member, addr);
                    return;
                }

                if (type.IsPointer) {
                    var val = (Pointer) value;
                    var unboxed = Pointer.Unbox(val);
                    if (unboxed != null) {
                        var unboxedAddr = (ulong) unboxed;
                        ClickToCopyText($"{(ulong) unboxed:X}");
                        if (beginModule > 0 && unboxedAddr >= beginModule && unboxedAddr <= endModule) {
                            ImGui.SameLine();
                            ImGui.PushStyleColor(ImGuiCol.Text, 0xffcbc0ff);
                            ClickToCopyText($"ffxiv_dx11.exe+{(unboxedAddr - beginModule):X}");
                            ImGui.PopStyleColor();
                        }

                        try {
                            var eType = type.GetElementType();
                            var ptrObj = Marshal.PtrToStructure(new IntPtr(unboxed), eType);
                            ImGui.SameLine();
                            PrintOutObject(ptrObj, (ulong) unboxed, new List<string>(path));
                        } catch {
                            // Ignored
                        }
                    } else {
                        ImGui.Text("null");
                    }
                } else {

                    if (type.IsArray) {

                        var arr = (Array) value;
                        if (ImGui.TreeNode($"Values##{member.Name}-{addr}-{string.Join("-", path)}")) {
                            for (var i = 0; i < arr.Length; i++) {
                                ImGui.Text($"[{i}]");
                                ImGui.SameLine();
                                PrintOutValue(addr, new List<string>(path) { $"_arrValue_{i}" }, type.GetElementType(), arr.GetValue(i), member);
                            }
                            ImGui.TreePop();
                        }
                        
                        
                    } else if (!type.IsPrimitive) {
                        switch (value) {
                            case Lumina.Text.SeString seString:
                                ImGui.Text($"{seString.RawString}");
                                break;
                            default:
                                PrintOutObject(value, addr, new List<string>(path));
                                break;
                        }
                    } else {
                        ImGui.Text($"{value}");
                    }
                }
            } catch (Exception ex) {
                ImGui.Text($"{{{ex}}}");
            }
            
        }
        
        public static unsafe void PrintOutObject(object obj, ulong addr, List<string> path, bool autoExpand = false, string headerText = null) {
            var pushedColor = 0;
            var openedNode = false;
            try {
                if (endModule == 0 && beginModule == 0) {
                    try {
                        beginModule = (ulong) Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64();
                        endModule = (beginModule + (ulong)Process.GetCurrentProcess().MainModule.ModuleMemorySize);
                    } catch {
                        endModule = 1;
                    }
                }
                
                ImGui.PushStyleColor(ImGuiCol.Text, 0xFF00FFFF);
                pushedColor++;
                if (autoExpand) {
                    ImGui.SetNextItemOpen(true, ImGuiCond.Appearing);
                }

                headerText ??= $"{obj}";

                if (ImGui.TreeNode($"{headerText}##print-obj-{addr:X}-{string.Join("-", path)}")) {
                    openedNode = true;
                    ImGui.PopStyleColor();
                    pushedColor--;
                    foreach (var f in obj.GetType().GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.Instance)) {

                        var fixedBuffer = (FixedBufferAttribute) f.GetCustomAttribute(typeof(FixedBufferAttribute));
                        if (fixedBuffer != null) {
                            ImGui.Text($"fixed");
                            ImGui.SameLine();
                            ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{fixedBuffer.ElementType.Name}[0x{fixedBuffer.Length:X}]");
                        } else {

                            if (f.FieldType.IsArray) {
                                var arr = (Array) f.GetValue(obj);
                                ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{f.FieldType.GetElementType()?.Name ?? f.FieldType.Name}[{arr.Length}]");
                            } else {
                                ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{f.FieldType.Name}");
                            }
                        }

                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.4f, 1), $"{f.Name}: ");
                        ImGui.SameLine();
                        
                        PrintOutValue(addr, new List<string>(path) { f.Name }, f.FieldType, f.GetValue(obj), f);
                    }

                    foreach (var p in obj.GetType().GetProperties()) {
                        ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.9f, 1), $"{p.PropertyType.Name}");
                        ImGui.SameLine();
                        ImGui.TextColored(new Vector4(0.2f, 0.6f, 0.4f, 1), $"{p.Name}: ");
                        ImGui.SameLine();
                        
                        PrintOutValue(addr, new List<string>(path) { p.Name }, p.PropertyType, p.GetValue(obj), p);
                    }

                    openedNode = false;
                    ImGui.TreePop();
                } else {
                    ImGui.PopStyleColor();
                    pushedColor--;
                }
            } catch (Exception ex) {
                ImGui.Text($"{{{ ex }}}");
            }
            
            if (openedNode) ImGui.TreePop();
            if (pushedColor > 0) ImGui.PopStyleColor(pushedColor);

        }
    }
}
