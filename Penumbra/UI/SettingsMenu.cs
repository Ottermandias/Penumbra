using System.Numerics;
using ImGuiNET;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private partial class SettingsMenu
        {
            private const string PenumbraSettingsLabel = "PenumbraSettings";

            private static readonly Vector2 MinSettingsSize = new( 800, 450 );
            private static readonly Vector2 MaxSettingsSize = new( 69420, 42069 );

            private readonly SettingsInterface _base;
            private readonly TabSettings       _settingsTab;
            private readonly TabImport         _importTab;
            private readonly TabBrowser        _browserTab;
            private readonly TabCharacters     _characterTab;
            public readonly  TabInstalled      InstalledTab;
            public readonly  TabEffective      EffectiveTab;

            public SettingsMenu( SettingsInterface ui )
            {
                _base         = ui;
                _settingsTab  = new TabSettings( _base );
                _importTab    = new TabImport( _base );
                _browserTab   = new TabBrowser();
                _characterTab = new TabCharacters( _base );
                InstalledTab  = new TabInstalled( _base );
                EffectiveTab  = new TabEffective( _base );
            }

#if DEBUG
            private const bool DefaultVisibility = true;
#else
            private const bool DefaultVisibility = false;
#endif
            public bool Visible = DefaultVisibility;

            public void Draw()
            {
                if( !Visible )
                {
                    return;
                }

                ImGui.SetNextWindowSizeConstraints( MinSettingsSize, MaxSettingsSize );
#if DEBUG
                var ret = ImGui.Begin( _base._plugin.PluginDebugTitleStr, ref Visible );
#else
                var ret = ImGui.Begin( _base._plugin.Name, ref Visible );
#endif
                if( !ret )
                {
                    return;
                }

                ImGui.BeginTabBar( PenumbraSettingsLabel );

                _settingsTab.Draw();
                _importTab.Draw();

                if( !_importTab.IsImporting() )
                {
                    _browserTab.Draw();
                    InstalledTab.Draw();
                    _characterTab.Draw();

                    if( _base._plugin.Configuration.ShowAdvanced )
                    {
                        EffectiveTab.Draw();
                    }
                }

                ImGui.EndTabBar();
                ImGui.End();
            }
        }
    }
}