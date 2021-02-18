using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Penumbra.Models;
using Penumbra.Util;
using Reloaded.Hooks.X64;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private partial class PluginDetails
        {
            private const string LabelDescEdit           = "##descedit";
            private const string LabelNewSingleGroup     = "New Single Group";
            private const string LabelNewSingleGroupEdit = "##newSingleGroup";
            private const string LabelNewMultiGroup      = "New Multi Group";
            private const string LabelGamePathsEdit      = "Game Paths";
            private const string LabelGamePathsEditBox   = "##gamePathsEdit";
            private const string ButtonAddToGroup        = "Add to Group";
            private const string ButtonRemoveFromGroup   = "Remove from Group";
            private const string TooltipAboutEdit        = "Use Ctrl+Enter for newlines.";
            private const string TextNoOptionAvailable   = "[Not Available]";
            private const string TextDefaultGamePath     = "default";
            private const char   GamePathsSeparator      = ';';

            private static readonly string TooltipFilesTabEdit =
                $"{TooltipFilesTab}\n" +
                $"Red Files are replaced in another group or a different option in this group, but not contained in the current option.";

            private static readonly string TooltipGamePathsEdit =
                $"Enter all game paths to add or remove, separated by '{GamePathsSeparator}'.\n" +
                $"Use '{TextDefaultGamePath}' to add the original file path.";

            private const float MultiEditBoxWidth = 300f;

            private bool DrawEditGroupSelector()
            {
                ImGui.SetNextItemWidth( OptionSelectionWidth );
                if( Meta.Groups.Count == 0 )
                {
                    ImGui.Combo( LabelGroupSelect, ref _selectedGroupIndex, TextNoOptionAvailable, 1 );
                    return false;
                }

                if( ImGui.Combo( LabelGroupSelect, ref _selectedGroupIndex
                    , Meta.Groups.Values.Select( G => G.GroupName ).ToArray()
                    , Meta.Groups.Count ) )
                {
                    SelectGroup();
                    SelectOption( 0 );
                }

                return true;
            }

            private bool DrawEditOptionSelector()
            {
                ImGui.SameLine();
                ImGui.SetNextItemWidth( OptionSelectionWidth );
                if( ( _selectedGroup?.Options.Count ?? 0 ) == 0 )
                {
                    ImGui.Combo( LabelOptionSelect, ref _selectedOptionIndex, TextNoOptionAvailable, 1 );
                    return false;
                }

                var group = ( InstallerInfo )_selectedGroup;
                if( ImGui.Combo( LabelOptionSelect, ref _selectedOptionIndex, group.Options.Select( O => O.OptionName ).ToArray(),
                    group.Options.Count ) )
                {
                    SelectOption();
                }

                return true;
            }

            private void DrawFileListTabEdit()
            {
                if( ImGui.BeginTabItem( LabelFileListTab ) )
                {
                    UpdateFilenameList();
                    if( ImGui.IsItemHovered() )
                    {
                        ImGui.SetTooltip( _editMode ? TooltipFilesTabEdit : TooltipFilesTab );
                    }

                    ImGui.SetNextItemWidth( -1 );
                    if( ImGui.ListBoxHeader( LabelFileListHeader, AutoFillSize - new Vector2( 0, 1.5f * ImGui.GetTextLineHeight() ) ) )
                    {
                        for( var i = 0; i < Mod.Mod.ModFiles.Count; ++i )
                        {
                            DrawFileAndGamePaths( i );
                        }
                    }

                    ImGui.ListBoxFooter();

                    DrawGroupRow();
                    ImGui.EndTabItem();
                }
                else
                {
                    _fullFilenameList = null;
                }
            }

            private bool DrawMultiSelectorEditBegin( InstallerInfo group )
            {
                var groupName = group.GroupName;
                if( ImGuiCustom.BeginFramedGroupEdit( ref groupName )
                    && groupName != group.GroupName && !Meta.Groups.ContainsKey( groupName ) )
                {
                    var oldConf = Mod.Settings[ group.GroupName ];
                    Meta.Groups.Remove( group.GroupName );
                    Mod.FixSpecificSetting( group.GroupName );
                    if( groupName.Length > 0 )
                    {
                        Meta.Groups[ groupName ] = new InstallerInfo()
                        {
                            GroupName     = groupName,
                            SelectionType = SelectType.Multi,
                            Options       = group.Options
                        };
                        Mod.Settings[ groupName ] = oldConf;
                    }

                    return true;
                }

                return false;
            }

            private void DrawMultiSelectorEditAdd( InstallerInfo group, float nameBoxStart )
            {
                var newOption = "";
                ImGui.SetCursorPosX( nameBoxStart );
                ImGui.SetNextItemWidth( MultiEditBoxWidth );
                if( ImGui.InputText( $"##new_{group.GroupName}_l", ref newOption, 64, ImGuiInputTextFlags.EnterReturnsTrue )
                    && newOption.Length != 0 )
                {
                    group.Options.Add( new Option()
                        { OptionName = newOption, OptionDesc = "", OptionFiles = new Dictionary< RelPath, HashSet< GamePath > >() } );
                    _selector.SaveCurrentMod();
                }
            }

            private void DrawMultiSelectorEdit( InstallerInfo group )
            {
                var nameBoxStart = CheckMarkSize;
                var flag         = Mod.Settings[ group.GroupName ];
                var modChanged   = DrawMultiSelectorEditBegin( group );

                for( var i = 0; i < group.Options.Count; ++i )
                {
                    var opt   = group.Options[ i ];
                    var label = $"##{group.GroupName}_{i}";
                    DrawMultiSelectorCheckBox( group, i, flag, label );

                    ImGui.SameLine();
                    var newName = opt.OptionName;

                    if( nameBoxStart == CheckMarkSize )
                    {
                        nameBoxStart = ImGui.GetCursorPosX();
                    }

                    ImGui.SetNextItemWidth( MultiEditBoxWidth );
                    if( ImGui.InputText( $"{label}_l", ref newName, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        if( newName.Length == 0 )
                        {
                            group.Options.RemoveAt( i );
                            var bitmaskFront = ( 1 << i ) - 1;
                            var bitmaskBack  = ~(bitmaskFront | ( 1 << i ));
                            Mod.Settings[ group.GroupName ] = ( flag & bitmaskFront ) | ( ( flag & bitmaskBack) >> 1 );
                            modChanged                      = true;
                        }
                        else if( newName != opt.OptionName )
                        {
                            group.Options[ i ] = new Option()
                                { OptionName = newName, OptionDesc = opt.OptionDesc, OptionFiles = opt.OptionFiles };
                            _selector.SaveCurrentMod();
                        }
                    }
                }

                DrawMultiSelectorEditAdd( group, nameBoxStart );

                if( modChanged )
                {
                    _selector.SaveCurrentMod();
                    Save();
                }

                ImGuiCustom.EndFramedGroup();
            }

            private bool DrawSingleSelectorEditGroup( InstallerInfo group, ref bool selectionChanged )
            {
                var groupName = group.GroupName;
                if( ImGui.InputText( $"##{groupName}_add", ref groupName, 64, ImGuiInputTextFlags.EnterReturnsTrue )
                    && !Meta.Groups.ContainsKey( groupName ) )
                {
                    var oldConf = Mod.Settings[ group.GroupName ];
                    if( groupName != group.GroupName )
                    {
                        Meta.Groups.Remove( group.GroupName );
                        selectionChanged |= Mod.FixSpecificSetting( group.GroupName );
                    }

                    if( groupName.Length > 0 )
                    {
                        Meta.Groups.Add( groupName, new InstallerInfo()
                        {
                            GroupName     = groupName,
                            Options       = group.Options,
                            SelectionType = SelectType.Single
                        } );
                        Mod.Settings[ groupName ] = oldConf;
                    }

                    return true;
                }

                return false;
            }

            private float DrawSingleSelectorEdit( InstallerInfo group )
            {
                var code             = Mod.Settings[ group.GroupName ];
                var selectionChanged = false;
                var modChanged       = false;
                if( ImGuiCustom.RenameableCombo( $"##{group.GroupName}", ref code, out var newName,
                    group.Options.Select( x => x.OptionName ).ToArray(), group.Options.Count ) )
                {
                    if( code == group.Options.Count )
                    {
                        if( newName.Length > 0 )
                        {
                            selectionChanged                = true;
                            modChanged                      = true;
                            Mod.Settings[ group.GroupName ] = code;
                            group.Options.Add( new Option()
                            {
                                OptionName  = newName,
                                OptionDesc  = "",
                                OptionFiles = new Dictionary< RelPath, HashSet< GamePath > >()
                            } );
                        }
                    }
                    else
                    {
                        if( newName.Length == 0 )
                        {
                            modChanged = true;
                            group.Options.RemoveAt( code );
                        }
                        else
                        {
                            if( newName != group.Options[ code ].OptionName )
                            {
                                modChanged = true;
                                group.Options[ code ] = new Option()
                                {
                                    OptionName  = newName, OptionDesc = group.Options[ code ].OptionDesc,
                                    OptionFiles = group.Options[ code ].OptionFiles
                                };
                            }
                            selectionChanged                |= Mod.Settings[ group.GroupName ] != code;
                            Mod.Settings[ group.GroupName ] =  code;
                        }
                        selectionChanged                |= Mod.FixSpecificSetting( group.GroupName );
                    }
                }

                ImGui.SameLine();
                var labelEditPos = ImGui.GetCursorPosX();
                modChanged |= DrawSingleSelectorEditGroup( group, ref selectionChanged );

                if( modChanged )
                {
                    _selector.SaveCurrentMod();
                }

                if( selectionChanged )
                {
                    Save();
                }

                return labelEditPos;
            }

            private void AddNewGroup( string newGroup, SelectType selectType )
            {
                if( Meta.Groups.ContainsKey( newGroup ) || newGroup.Length <= 0 )
                {
                    return;
                }

                Meta.Groups[ newGroup ] = new InstallerInfo()
                {
                    GroupName     = newGroup,
                    SelectionType = selectType,
                    Options       = new List< Option >()
                };

                Mod.Settings[ newGroup ] = 0;
                _selector.SaveCurrentMod();
                Save();
            }

            private void DrawAddSingleGroupField( float labelEditPos )
            {
                var newGroup = "";
                if( labelEditPos == CheckMarkSize )
                {
                    ImGui.SetCursorPosX( CheckMarkSize );
                    ImGui.SetNextItemWidth( MultiEditBoxWidth );
                    if( ImGui.InputText( LabelNewSingleGroup, ref newGroup, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        AddNewGroup( newGroup, SelectType.Single );
                    }
                }
                else
                {
                    ImGuiCustom.RightJustifiedLabel( labelEditPos, LabelNewSingleGroup );
                    if( ImGui.InputText( LabelNewSingleGroupEdit, ref newGroup, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                    {
                        AddNewGroup( newGroup, SelectType.Single );
                    }
                }
            }

            private void DrawAddMultiGroupField()
            {
                var newGroup = "";
                ImGui.SetCursorPosX( CheckMarkSize );
                ImGui.SetNextItemWidth( MultiEditBoxWidth );
                if( ImGui.InputText( LabelNewMultiGroup, ref newGroup, 64, ImGuiInputTextFlags.EnterReturnsTrue ) )
                {
                    AddNewGroup( newGroup, SelectType.Multi );
                }
            }

            private void DrawGroupSelectorsEdit()
            {
                var labelEditPos = CheckMarkSize;
                foreach( var g in Meta.Groups.Values.Where( g => g.SelectionType == SelectType.Single ) )
                {
                    labelEditPos = DrawSingleSelectorEdit( g );
                }

                DrawAddSingleGroupField( labelEditPos );

                foreach( var g in Meta.Groups.Values.Where( g => g.SelectionType == SelectType.Multi ) )
                {
                    DrawMultiSelectorEdit( g );
                }

                DrawAddMultiGroupField();
            }
        }
    }
}