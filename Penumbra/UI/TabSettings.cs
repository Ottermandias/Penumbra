using System.Diagnostics;
using ImGuiNET;

namespace Penumbra.UI
{
    public partial class SettingsInterface
    {
        private class TabSettings
        {
            private const string LabelTab                  = "Settings";
            private const string LabelRootFolder           = "Root Folder";
            private const string LabelRediscoverButton     = "Rediscover Mods";
            private const string LabelOpenFolder           = "Open Mods Folder";
            private const string LabelEnabled              = "Enable Mods";
            private const string LabelEnabledPlayerWatch   = "Enable automatic Character Redraws";
            private const string LabelInvertModOrder       = "Invert mod load order (mods are loaded bottom up)";
            private const string LabelShowAdvanced         = "Show Advanced Settings";
            private const string LabelLogLoadedFiles       = "Log all loaded files";
            private const string LabelDisableNotifications = "Disable filesystem change notifications";
            private const string LabelEnableHttpApi        = "Enable HTTP API";
            private const string LabelReloadResource       = "Reload Player Resource";
                
            private readonly SettingsInterface _base;
            private readonly Configuration     _config;
            private bool                       _configChanged;

            public TabSettings(SettingsInterface ui)
            {
                _base          = ui;
                _config        = _base._plugin.Configuration;
                _configChanged = false;
            }

            private void DrawRootFolder()
            {
                var basePath = _config.CurrentCollection;
                if( ImGui.InputText( LabelRootFolder, ref basePath, 255 ) && _config.CurrentCollection != basePath )
                {
                    _config.CurrentCollection = basePath;
                    _configChanged = true;
                }
            }

            private void DrawRediscoverButton()
            {
                if( ImGui.Button( LabelRediscoverButton ) )
                {
                    _base.ReloadMods();
                    _base._menu._installedTab._selector.ClearSelection();
                }
            }

            private void DrawOpenModsButton()
            {
                if( ImGui.Button( LabelOpenFolder ) )
                {
                    Process.Start( _config.CurrentCollection );
                }
            }

            private void DrawEnabledBox()
            {
                var enabled = _config.IsEnabled;
                if( ImGui.Checkbox( LabelEnabled, ref enabled ) )
                {
                    _config.IsEnabled = enabled;
                    _configChanged = true;
                    if (enabled)
                        _base._plugin.ActorRefresher.UnloadAtOnceRedrawWithSettings();
                    else
                        _base._plugin.ActorRefresher.UnloadAtOnceRedrawWithoutSettings();
                }
            }

            private void DrawEnabledPlayerWatcher()
            {
                var enabled = _config.EnableActorWatch;
                if( ImGui.Checkbox( LabelEnabledPlayerWatch, ref enabled ) )
                {
                    _config.EnableActorWatch = enabled;
                    _configChanged = true;
                    if (enabled)
                        _base._plugin.ActorWatcher.EnableActorWatch();
                    else
                        _base._plugin.ActorWatcher.DisableActorWatch();
                }
            }

            private void DrawInvertModOrderBox()
            {
                var invertOrder = _config.InvertModListOrder;
                if( ImGui.Checkbox( LabelInvertModOrder, ref invertOrder ) )
                {
                    _config.InvertModListOrder = invertOrder;
                    _base.ReloadMods();
                    _configChanged = true;
                }
            }

            private void DrawShowAdvancedBox()
            {
                var showAdvanced = _config.ShowAdvanced;
                if( ImGui.Checkbox( LabelShowAdvanced, ref showAdvanced ) )
                {
                    _config.ShowAdvanced = showAdvanced;
                    _configChanged = true;
                    _base._menu._effectiveTab.RebuildFileList(showAdvanced);
                }
            }

            private void DrawLogLoadedFilesBox()
            {
                if( _base._plugin.ResourceLoader != null )
                    ImGui.Checkbox( LabelLogLoadedFiles, ref _base._plugin.ResourceLoader.LogAllFiles );
            }

            private void DrawDisableNotificationsBox()
            {
                var fswatch = _config.DisableFileSystemNotifications;
                if( ImGui.Checkbox( LabelDisableNotifications, ref fswatch ) )
                {
                    _config.DisableFileSystemNotifications = fswatch;
                    _configChanged = true;
                }
            }

            private void DrawEnableHttpApiBox()
            {
                var http = _config.EnableHttpApi;
                if( ImGui.Checkbox( LabelEnableHttpApi, ref http ) )
                {
                    if( http )
                        _base._plugin.CreateWebServer();
                    else
                        _base._plugin.ShutdownWebServer();

                    _config.EnableHttpApi = http;
                    _configChanged = true;
                }
            }

            private void DrawReloadResourceButton()
            {
                if( ImGui.Button( LabelReloadResource ) )
                {
                    _base._plugin.GameUtils.ReloadPlayerResources();
                }
            }

            private void DrawAdvancedSettings()
            {
                DrawLogLoadedFilesBox();
                DrawDisableNotificationsBox();
                DrawEnableHttpApiBox();
                DrawReloadResourceButton();
            }

            public void Draw()
            {
                var ret = ImGui.BeginTabItem( LabelTab );
                if( !ret )
                    return;

                DrawRootFolder();

                DrawRediscoverButton();
                ImGui.SameLine();
                DrawOpenModsButton();

                ImGuiCustom.VerticalDistance(DefaultVerticalSpace);
                DrawEnabledBox();
                DrawEnabledPlayerWatcher();

                ImGuiCustom.VerticalDistance(DefaultVerticalSpace);
                DrawInvertModOrderBox();
                    
                ImGuiCustom.VerticalDistance(DefaultVerticalSpace);
                DrawShowAdvancedBox();
                    
                if( _config.ShowAdvanced )
                    DrawAdvancedSettings();

                if( _configChanged )
                {
                    _config.Save();
                    _configChanged = false;
                }

                ImGui.EndTabItem();
            }
        }
    }
}