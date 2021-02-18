using System.IO;
using System.Linq;
using ImGuiNET;
using Penumbra.Mods;
using Penumbra.Util;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabEffective
        {
            private const string LabelTab        = "Effective File List";
            private const float  TextSizePadding = 5f;

            private readonly ModManager _mods;
            private          float      _maxGamePath;

            public TabEffective( SettingsInterface ui )
            {
                _mods = ui._plugin.ModManager;
                RebuildFileList( ui._plugin.Configuration.ShowAdvanced );
            }

            public void RebuildFileList( bool advanced )
            {
                if( advanced )
                {
                    _maxGamePath = TextSizePadding + ( _mods.DefaultResolvedFiles.Count > 0
                        ? _mods.DefaultResolvedFiles.Keys.Max( f => ImGui.CalcTextSize( f ).X )
                        : 0f );
                }
                else
                {
                    _maxGamePath = 0f;
                }
            }

            private void DrawFileLine( FileInfo file, GamePath path )
            {
                ImGui.Selectable( path );
                ImGui.SameLine();
                ImGui.SetCursorPosX( _maxGamePath );
                ImGui.TextUnformatted( "  <-- " );
                ImGui.SameLine();
                ImGui.Selectable( file.FullName );
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                {
                    return;
                }

                if( ImGui.ListBoxHeader( "##effective_files", AutoFillSize ) )
                {
                    foreach( var file in _mods.DefaultResolvedFiles )
                    {
                        DrawFileLine( file.Value, file.Key );
                    }

                    ImGui.ListBoxFooter();
                }

                ImGui.EndTabItem();
            }
        }
    }
}