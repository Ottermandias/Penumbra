using System.Linq;
using System.IO;
using ImGuiNET;
using Penumbra.Mods;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabEffective
        {
            private const string LabelTab        = "Effective File List";
            private const float  TextSizePadding = 5f;

            private ModManager           _mods;
            private (FileInfo, GamePath)[] _fileList    = null;
            private float                _maxGamePath = 0f;

            public TabEffective(SettingsInterface ui)
            { 
                _mods = ui._plugin.ModManager; 
                RebuildFileList(ui._plugin.Configuration.ShowAdvanced); 
            }

            public void RebuildFileList(bool advanced)
            {
                if (advanced)
                {
                    _fileList    = _mods.DefaultResolvedFiles.Select( P => (P.Value, P.Key) ).ToArray();
                    _maxGamePath = ((_fileList.Length > 0) ? _fileList.Max( P => ImGui.CalcTextSize(P.Item2).X ) : 0f) + TextSizePadding;
                }
                else
                {
                    _fileList    = null;
                    _maxGamePath = 0f;
                }
            }

            private void DrawFileLine((FileInfo, GamePath) file)
            {
                ImGui.Selectable(file.Item2);
                ImGui.SameLine();
                ImGui.SetCursorPosX(_maxGamePath);
                ImGui.TextUnformatted("  <-- ");
                ImGui.SameLine();
                ImGui.Selectable(file.Item1.FullName);
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                    return;

                if( ImGui.ListBoxHeader( "##effective_files", AutoFillSize ) )
                {
                    foreach( var file in _fileList )
                        DrawFileLine(file);

                    ImGui.ListBoxFooter();
                }

                ImGui.EndTabItem();
            }
        }
    }
}