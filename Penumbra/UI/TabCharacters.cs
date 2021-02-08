using ImGuiNET;
using Penumbra.Models;
using Penumbra.Mods;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System;
using Dalamud.Plugin;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabCharacters
        {
            private const string LabelTab            = "Character Config";
            private const string LabelSelectorList   = "##charSelection";
            private const float  SelectorPanelWidth  = 240f;
            private const uint   DisabledCharColor   = 0xFF666666;
            private const float  InputTextWidth      = -1;
            private const float  InputPriorityWidth  = 120;

            private readonly SettingsInterface _base;
            private Dictionary<string, CharacterSettings> List { get{ return _base._plugin.ModManager.CharacterSettings.CharacterConfigs; }}
            public TabCharacters(SettingsInterface ui) => _base = ui;

            private string _newCharName = "";
            private int    _currentChar = 0;

            private void DrawSelector()
            {
                ImGui.BeginGroup();
                var ret = ImGui.BeginChild( LabelSelectorList, new Vector2(SelectorPanelWidth, -2 * ImGui.GetFrameHeightWithSpacing() ), true );
                if (!ret)
                    return;

                for (var i = 0; i < List.Count; ++i)
                {
                    var changedColor = false;
                    var (name, settings) = (List.ElementAt(i).Key, List.ElementAt(i).Value);
                    if (!settings.Enabled)
                    {
                        ImGui.PushStyleColor( ImGuiCol.Text, DisabledCharColor );
                        changedColor = true;
                    }

                    if (ImGui.Selectable(name, i == _currentChar))
                    {
                        _currentChar = i;
                    }

                    if( changedColor )
                        ImGui.PopStyleColor();
                }
               
                ImGui.EndChild();

                ImGui.SetNextItemWidth(SelectorPanelWidth);
                ImGui.InputText("##inputName", ref _newCharName, 40);
                if (ImGui.Button("Add Default") && _newCharName.Length > 0 && !List.ContainsKey(_newCharName))
                {
                    List[_newCharName] = CharacterSettings.ConvertFromDefault(_base._plugin.Configuration.InvertModListOrder, _base._plugin.ModManager.Mods.ModSettings);
                    CharacterSettingList.SaveToFile(_newCharName, List[_newCharName], CharacterSettingsFile(_newCharName));
                    _base._plugin.ActorWatcher.AddPlayerToWatch(_newCharName);
                }
                ImGui.SameLine();
                if (ImGui.Button("Add Selected") && _newCharName.Length > 0 && _currentChar < List.Count && !List.ContainsKey(_newCharName))
                {
                    List[_newCharName] = List.ElementAt(_currentChar).Value.Copy();
                    CharacterSettingList.SaveToFile(_newCharName, List[_newCharName], CharacterSettingsFile(_newCharName));
                    _base._plugin.ActorWatcher.AddPlayerToWatch(_newCharName);
                }
                ImGui.SameLine();
                if (ImGui.Button("Delete") && _currentChar < List.Count)
                {
                    try
                    {
                        var name = List.ElementAt(_currentChar).Key;
                        List.Remove(name);
                        var file = CharacterSettingsFile(name);
                        _base._plugin.ActorWatcher.RemovePlayerFromWatch(name);
                        if (file.Exists)
                            file.Delete();
                    }
                    catch(Exception e)
                    {
                        PluginLog.Error($"Could not delete file:\n{e}");
                    }
                }
                ImGui.EndGroup();
            }

            private void DrawSettingInfo()
            {
                if (_currentChar >= List.Count)
                    return;

                var ret = ImGui.BeginChild( "##CharPanel", AutoFillSize, true );
                if (!ret)
                    return;

                var name = List.ElementAt(_currentChar).Key;
                var conf = List.ElementAt(_currentChar).Value;

                var charEnabled = conf.Enabled;
                if (ImGui.Checkbox("Enable Character", ref charEnabled))
                {
                    conf.Enabled = charEnabled;
                    
                    CharacterSettingList.SaveToFile(name, conf, CharacterSettingsFile(name));
                }

                ImGui.SameLine();
                var inversedOrder = conf.InvertOrder;
                if (ImGui.Checkbox("Inverse Mod Order", ref inversedOrder))
                {
                    conf.InvertOrder = inversedOrder;
                    CharacterSettingList.SaveToFile(name, conf, CharacterSettingsFile(name));
                }

                foreach (var mod in conf.ModSettingsJson.ToArray())
                {
                    var modChanged = false;
                    var modName = mod.Key;
                    if (ImGuiCustom.BeginFramedGroupEdit(ref modName))
                    {
                        if(modName != mod.Key && !conf.ModSettingsJson.ContainsKey(modName))
                        {
                            conf.ModSettingsJson.Add(modName, mod.Value);
                        conf.ModSettingsJson.Remove(mod.Key);
                        modChanged = true;
                        }
                    }
                    var tmpPriority = mod.Value.Priority;
                    ImGui.SetNextItemWidth(InputPriorityWidth);
                    if (ImGui.InputInt($"Priority##{modName}", ref tmpPriority))
                    {
                        if (mod.Value.Priority != tmpPriority)
                        {
                            mod.Value.Priority = tmpPriority;
                            modChanged = true;
                        }
                    }
                    foreach (var group in mod.Value.Options.ToArray())
                    {
                        var groupName = group.Key;
                        ImGui.SetNextItemWidth(InputTextWidth);
                        if (ImGui.InputText($"##{groupName}", ref groupName, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            if (groupName != group.Key && !mod.Value.Options.ContainsKey(groupName))
                            {
                                mod.Value.Options.Add(groupName, group.Value);
                                mod.Value.Options.Remove(group.Key);
                                modChanged = true;
                            }
                        }
                        ImGui.Indent(40);
                        foreach (var option in group.Value.ToArray())
                        {
                            var optionName = option;
                            ImGui.SetNextItemWidth(InputTextWidth);
                            if (ImGui.InputText($"##{groupName}_{option}", ref optionName, 64, ImGuiInputTextFlags.EnterReturnsTrue))
                            {
                                if (optionName != option && !group.Value.Contains(optionName))
                                {
                                    group.Value.Add(optionName);
                                    group.Value.Remove(option);
                                    modChanged = true;
                                }
                            }
                        }
                        ImGui.Unindent(40);
                    }
                    
                    ImGuiCustom.EndFramedGroup();
                    if (modChanged)
                    {
                        conf.ComputeModSettings(_base._plugin.ModManager.Mods.ModSettings);
                        conf.RenewFiles(_base._plugin.ModManager.Mods.ModSettings);
                        CharacterSettingList.SaveToFile(name, conf, CharacterSettingsFile(name));
                    }
                }

                ImGui.EndChild();
            }

            private FileInfo CharacterSettingsFile(string name)
            {
                name = new string(name.Where( c => !Path.GetInvalidPathChars().Contains(c)).ToArray()).Replace(' ', '_').ToLowerInvariant();
                return new(Path.Combine(_base._plugin.Configuration.CurrentCollection, $"charconfig_{name}.json"));
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                    return;

                DrawSelector();
                ImGui.SameLine();
                DrawSettingInfo();

                ImGui.EndTabItem();
                return;
            }
        }
    }
}