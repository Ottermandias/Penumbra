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

                var changedColor = false;

                for (var i = 0; i < List.Count; ++i)
                {
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
                }
                ImGui.SameLine();
                if (ImGui.Button("Add Selected") && _newCharName.Length > 0 && _currentChar < List.Count && !List.ContainsKey(_newCharName))
                {
                    List[_newCharName] = List.ElementAt(_currentChar).Value;
                    CharacterSettingList.SaveToFile(_newCharName, List[_newCharName], CharacterSettingsFile(_newCharName));
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

                foreach (var mod in conf.ModSettings)
                {
                    ImGui.Selectable($"{mod.Value.Priority} - {mod.Key}");
                    ImGui.Indent(40);
                    foreach (var group in mod.Value.Options)
                    {
                        ImGui.Selectable($"{group.Value} - {group.Key}");
                    }
                    ImGui.Unindent(40);
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