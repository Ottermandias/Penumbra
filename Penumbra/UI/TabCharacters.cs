using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Dalamud.Plugin;
using ImGuiNET;
using Penumbra.Models;
using Penumbra.Mods;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabCharacters
        {
            private const string LabelTab           = "Character Config";
            private const string LabelSelectorList  = "##charSelection";
            private const string LabelNewCharInput  = "##inputName";
            private const string LabelCharPanel     = "##charPanel";
            private const string ButtonAddDefault   = "Add Default";
            private const string ButtonAddSelected  = "Add Selected";
            private const string ButtonDeleteChar   = "Delete";
            private const string CheckBoxEnableChar = "Enable Character";
            private const string CheckBoxInvertMods = "Invert Mod Order";
            private const float  SelectorPanelWidth = 240f;
            private const uint   DisabledCharColor  = 0xFF666666;
            private const float  InputTextWidth     = -1;
            private const float  InputPriorityWidth = 120;

            private readonly SettingsInterface _base;

            private Dictionary< string, CharacterSettings > List
                => _base._plugin.ModManager.CharacterSettings.CharacterConfigs;

            public TabCharacters( SettingsInterface ui )
                => _base = ui;

            private string _newCharName = "";
            private int    _currentChar = 0;

            private void DrawSelector()
            {
                ImGui.BeginGroup();
                var ret = ImGui.BeginChild( LabelSelectorList
                    , new Vector2( SelectorPanelWidth, -2 * ImGui.GetFrameHeightWithSpacing() ), true );
                if( !ret )
                {
                    return;
                }

                for( var i = 0; i < List.Count; ++i )
                {
                    var changedColor = false;
                    var (name, settings) = ( List.ElementAt( i ).Key, List.ElementAt( i ).Value );
                    if( !settings.Enabled )
                    {
                        ImGui.PushStyleColor( ImGuiCol.Text, DisabledCharColor );
                        changedColor = true;
                    }

                    if( ImGui.Selectable( name, i == _currentChar ) )
                    {
                        _currentChar = i;
                    }

                    if( changedColor )
                    {
                        ImGui.PopStyleColor();
                    }
                }

                ImGui.EndChild();

                ImGui.SetNextItemWidth( SelectorPanelWidth );
                ImGui.InputText( LabelNewCharInput, ref _newCharName, 40 );
                if( ImGui.Button( ButtonAddDefault ) && _newCharName.Length > 0
                    && !List.ContainsKey( _newCharName ) )
                {
                    List[ _newCharName ] = CharacterSettings.ConvertFromDefault( _base._plugin.Configuration.InvertModListOrder,
                        _base._plugin.ModManager.Mods.ModSettings );
                    CharacterSettingList.SaveToFile( _newCharName, List[ _newCharName ], CharacterSettingsFile( _newCharName ) );
                    _base._plugin.PlayerWatcher.AddPlayerToWatch( _newCharName );
                }

                ImGui.SameLine();
                if( ImGui.Button( ButtonAddSelected ) && _newCharName.Length > 0
                    && _currentChar < List.Count && !List.ContainsKey( _newCharName ) )
                {
                    List[ _newCharName ] = List.ElementAt( _currentChar ).Value.Copy();
                    CharacterSettingList.SaveToFile( _newCharName, List[ _newCharName ], CharacterSettingsFile( _newCharName ) );
                    _base._plugin.PlayerWatcher.AddPlayerToWatch( _newCharName );
                }

                ImGui.SameLine();
                if( ImGui.Button( ButtonDeleteChar ) && _currentChar < List.Count )
                {
                    try
                    {
                        var name = List.ElementAt( _currentChar ).Key;
                        List.Remove( name );
                        var file = CharacterSettingsFile( name );
                        _base._plugin.PlayerWatcher.RemovePlayerFromWatch( name );
                        if( file.Exists )
                        {
                            file.Delete();
                        }
                    }
                    catch( Exception e )
                    {
                        PluginLog.Error( $"Could not delete file:\n{e}" );
                    }
                }

                ImGui.EndGroup();
            }


            private bool BeginModGroup( CharacterSettings conf, string modName, ModSettingsNames settings )
            {
                var tmp = modName;
                if( ImGuiCustom.BeginFramedGroupEdit( ref tmp )
                    && tmp != modName && !conf.ModSettingsJson.ContainsKey( modName ) )
                {
                    conf.ModSettingsJson.Add( tmp, settings );
                    conf.ModSettingsJson.Remove( modName );
                    return true;
                }

                return false;
            }

            private static bool DrawPrioritySetter( string modName, ModSettingsNames settings )
            {
                var tmpPriority = settings.Priority;
                ImGui.SetNextItemWidth( InputPriorityWidth );
                if( ImGui.InputInt( $"Priority##{modName}", ref tmpPriority )
                    && settings.Priority != tmpPriority )
                {
                    settings.Priority = tmpPriority;
                    return true;
                }

                return false;
            }

            private bool DrawGroupHeader( string groupName, ModSettingsNames settings, HashSet< string > group )
            {
                var tmp = groupName;
                ImGui.SetNextItemWidth( InputTextWidth );
                if( ImGui.InputText( $"##{groupName}", ref tmp, 64, ImGuiInputTextFlags.EnterReturnsTrue )
                    && groupName != tmp && !settings.Settings.ContainsKey( groupName ) )
                {
                    settings.Settings.Add( tmp, group );
                    settings.Settings.Remove( groupName );
                    return true;
                }

                return false;
            }

            private bool DrawOptionLine( string groupName, string optionName, HashSet< string > group )
            {
                var tmp = optionName;
                ImGui.SetNextItemWidth( InputTextWidth );
                if( ImGui.InputText( $"##{groupName}_{optionName}", ref tmp, 64, ImGuiInputTextFlags.EnterReturnsTrue )
                    && optionName != tmp && !group.Contains( optionName ) )
                {
                    group.Add( tmp );
                    group.Remove( optionName );
                    return true;
                }

                return false;
            }

            private void DrawModPanel( string characterName, CharacterSettings conf, string modName, ModSettingsNames settings )
            {
                var modChanged = BeginModGroup( conf, modName, settings );
                modChanged |= DrawPrioritySetter( modName, settings );

                foreach( var group in settings.Settings.ToArray() )
                {
                    modChanged |= DrawGroupHeader( group.Key, settings, group.Value );
                    ImGui.Indent( 40 );
                    foreach( var option in group.Value.ToArray() )
                    {
                        modChanged |= DrawOptionLine( group.Key, option, group.Value );
                    }

                    ImGui.Unindent( 40 );
                }

                if( modChanged )
                {
                    conf.ComputeModSettings( _base._plugin.ModManager.Mods.ModSettings );
                    conf.RenewFiles( _base._plugin.ModManager.Mods.ModSettings );
                    CharacterSettingList.SaveToFile( characterName, conf, CharacterSettingsFile( characterName ) );
                }

                ImGuiCustom.EndFramedGroup();
            }

            private void DrawSettingInfo()
            {
                if( _currentChar >= List.Count )
                {
                    return;
                }

                var ret = ImGui.BeginChild( LabelCharPanel, AutoFillSize, true );
                if( !ret )
                {
                    return;
                }

                var name = List.ElementAt( _currentChar ).Key;
                var conf = List.ElementAt( _currentChar ).Value;

                var charEnabled = conf.Enabled;
                if( ImGui.Checkbox( CheckBoxEnableChar, ref charEnabled ) )
                {
                    conf.Enabled = charEnabled;
                    CharacterSettingList.SaveToFile( name, conf, CharacterSettingsFile( name ) );
                }

                ImGui.SameLine();
                var inverseOrder = conf.InvertOrder;
                if( ImGui.Checkbox( CheckBoxInvertMods, ref inverseOrder ) )
                {
                    conf.InvertOrder = inverseOrder;
                    CharacterSettingList.SaveToFile( name, conf, CharacterSettingsFile( name ) );
                }

                foreach( var mod in conf.ModSettingsJson.ToArray() )
                {
                    DrawModPanel( name, conf, mod.Key, mod.Value );
                }

                ImGui.EndChild();
            }

            private FileInfo CharacterSettingsFile( string name )
            {
                name = name.ReplaceInvalidPathSymbols();
                return new FileInfo( Path.Combine( _base._plugin.Configuration.CurrentCollection, $"charconfig_{name}.json" ) );
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                DrawSelector();
                ImGui.SameLine();
                DrawSettingInfo();

                ImGui.EndTabItem();
            }
        }
    }
}
