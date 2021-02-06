using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ModManager : IDisposable
    {
        private readonly Plugin _plugin;
        public readonly Dictionary<GamePath, FileInfo > ResolvedFiles = new();
        public readonly Dictionary<GamePath, GamePath > SwappedFiles = new();

        public ModCollection Mods { get; set; }

        private DirectoryInfo _basePath;

        public ModManager( Plugin plugin )
        {
            _plugin = plugin;
        }

        public void DiscoverMods()
        {
            if( _basePath == null )
            {
                return;
            }

            DiscoverMods( _basePath );
        }

//         private void FileSystemWatcherOnChanged( object sender, FileSystemEventArgs e )
//         {
// #if DEBUG
//             PluginLog.Verbose( "file changed: {FullPath}", e.FullPath );
// #endif
//
//             if( _plugin.ImportInProgress )
//             {
//                 return;
//             }
//
//             if( _plugin.Configuration.DisableFileSystemNotifications )
//             {
//                 return;
//             }
//
//             var file = e.FullPath;
//
//             if( !ResolvedFiles.Any( x => x.Value.FullName == file ) )
//             {
//                 return;
//             }
//
//             PluginLog.Log( "a loaded file has been modified - file: {FullPath}", file );
//             _plugin.GameUtils.ReloadPlayerResources();
//         }

        public void DiscoverMods( string basePath )
        {
            DiscoverMods( new DirectoryInfo( basePath ) );
        }

        public void DiscoverMods( DirectoryInfo basePath )
        {
            if( basePath == null )
            {
                return;
            }

            if( !basePath.Exists )
            {
                Mods = null;
                return;
            }

            _basePath = basePath;

            // haha spaghet
            // _fileSystemWatcher?.Dispose();
            // _fileSystemWatcher = new FileSystemWatcher( _basePath.FullName )
            // {
            //     NotifyFilter = NotifyFilters.LastWrite |
            //                    NotifyFilters.FileName |
            //                    NotifyFilters.DirectoryName,
            //     IncludeSubdirectories = true,
            //     EnableRaisingEvents = true
            // };
            //
            // _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
            // _fileSystemWatcher.Created += FileSystemWatcherOnChanged;
            // _fileSystemWatcher.Deleted += FileSystemWatcherOnChanged;
            // _fileSystemWatcher.Renamed += FileSystemWatcherOnChanged;

            Mods = new ModCollection( basePath );
            Mods.Load();
            Mods.Save();

            CalculateEffectiveFileList();

            // Needed to reload body textures with mods
            //_plugin.GameUtils.ReloadPlayerResources();
        }

        public void CalculateEffectiveFileList()
        {
            ResolvedFiles.Clear();
            SwappedFiles.Clear();

            var registeredFiles = new Dictionary<GamePath, string >();

            foreach( var (mod, settings) in Mods.GetOrderedAndEnabledModListWithSettings( _plugin.Configuration.InvertModListOrder ) )
            {
                mod.FileConflicts?.Clear();

                if(settings.Conf == null) {
                    settings.Conf = new();
                    _plugin.ModManager.Mods.Save();
                }

                foreach( var file in mod.ModFiles )
                {
                    RelPath relativeFilePath = new(file, mod.ModBasePath);
                    
                    var doNotAdd = false;
                    void AddFiles(HashSet<GamePath> gamePaths)
                    {
                        doNotAdd = true;
                        foreach (var gamePath in gamePaths)
                        {
                            if( !ResolvedFiles.ContainsKey( gamePath ) )
                            {
                                ResolvedFiles[gamePath] = file;
                                registeredFiles[gamePath] = mod.Meta.Name;
                            }
                            else if( registeredFiles.TryGetValue( gamePath, out var modName ) )
                            {
                                mod.AddConflict( modName, gamePath );
                            }
                        }
                    }

                    HashSet<GamePath> paths;
                    foreach (var group in mod.Meta.Groups.Values)
                    {
                        if (!settings.Conf.TryGetValue(group.GroupName, out var setting) 
                            || (group.SelectionType == SelectType.Single && settings.Conf[group.GroupName] >= group.Options.Count))
                        {
                            settings.Conf[group.GroupName] = 0;
                            _plugin.ModManager.Mods.Save();
                            setting = 0;
                        }

                        if (group.Options.Count == 0)
                            continue;

                        if (group.SelectionType == SelectType.Multi)
                            settings.Conf[group.GroupName] &= ((1 << group.Options.Count) - 1);
                        
                        switch(group.SelectionType)
                        {
                            case SelectType.Single:
                                if (group.Options[setting].OptionFiles.TryGetValue(relativeFilePath, out paths))
                                    AddFiles(paths);
                                else
                                {
                                    for(var i = 0; i < group.Options.Count; ++i)
                                    {
                                        if (i == setting)
                                            continue;
                                        if(group.Options[i].OptionFiles.ContainsKey(relativeFilePath))
                                        {
                                            doNotAdd = true;
                                            break;
                                        }
                                    }
                                }
                                break;
                            case SelectType.Multi:
                                for(var i = 0; i < group.Options.Count; ++i)
                                {
                                    if ((setting & (1 << i)) != 0)
                                    {
                                        if (group.Options[i].OptionFiles.TryGetValue(relativeFilePath, out paths))
                                            AddFiles(paths);
                                    }
                                    else if (group.Options[i].OptionFiles.ContainsKey(relativeFilePath))
                                        doNotAdd = true;
                                }
                                break;
                        }
                    }
                    if (!doNotAdd)
                        AddFiles(new() { new GamePath(relativeFilePath) });
                }

                foreach( var swap in mod.Meta.FileSwaps )
                {
                    // just assume people put not fucked paths in here lol
                    if( !SwappedFiles.ContainsKey( swap.Value ) )
                    {
                        SwappedFiles[ swap.Key ] = swap.Value;
                        registeredFiles[ swap.Key ] = mod.Meta.Name;
                    }
                    else if( registeredFiles.TryGetValue( swap.Key, out var modName ) )
                    {
                        mod.AddConflict( modName, swap.Key );
                    }
                }
            }
            _plugin.GameUtils.ReloadPlayerResources();
        }

        public void ChangeModPriority( ModInfo info, bool up = false )
        {
            Mods.ReorderMod( info, up );
            CalculateEffectiveFileList();
        }

        public void DeleteMod( ResourceMod mod )
        {
            Directory.Delete( mod.ModBasePath.FullName, true );
            DiscoverMods();
        }

        public FileInfo GetCandidateForGameFile( GamePath gameResourcePath )
        {
            var val = ResolvedFiles.TryGetValue( gameResourcePath, out var candidate );
            if( !val )
            {
                return null;
            }

            if( candidate.FullName.Length >= 260 || !candidate.Exists )
            {
                return null;
            }

            return candidate;
        }

        public GamePath GetSwappedFilePath( GamePath gameResourcePath )
        {
            return SwappedFiles.TryGetValue( gameResourcePath, out var swappedPath ) ? swappedPath : GamePath.GenerateUnchecked(null);
        }

        public string ResolveSwappedOrReplacementFilePath( GamePath gameResourcePath )
        {
            return GetCandidateForGameFile( gameResourcePath )?.FullName ?? GetSwappedFilePath( gameResourcePath );
        }


        public void Dispose()
        {
            // _fileSystemWatcher?.Dispose();
        }
    }
}
