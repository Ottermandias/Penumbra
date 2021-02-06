using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Penumbra.Models;

namespace Penumbra.Mods
{

    public class ResolvedFiles : Dictionary<GamePath, FileInfo>{ }
    public class  SwappedFiles : Dictionary<GamePath, GamePath>{ }

    public class ModManager : IDisposable
    {
        private readonly Plugin _plugin;
        private DirectoryInfo   _basePath;

        public readonly CharacterSettingList CharacterSettings = new();
        public readonly ResolvedFiles DefaultResolvedFiles = new();
        public readonly  SwappedFiles DefaultSwappedFiles  = new();

        public ResolvedFiles CurrentResolvedFiles;
        public  SwappedFiles CurrentSwappedFiles;

        public ModCollection Mods { get; set; }

        public ModManager( Plugin plugin )
        {
            _plugin = plugin;
            RestoreDefaultFileLists();
        }

        public void ExchangeFileLists(ResolvedFiles resolved, SwappedFiles swapped)
        {
            CurrentResolvedFiles = resolved;
            CurrentSwappedFiles = swapped;
        }

        public void RestoreDefaultFileLists() => ExchangeFileLists(DefaultResolvedFiles, DefaultSwappedFiles);

        public void DiscoverMods()
        {
            if( _basePath != null )
                DiscoverMods( _basePath );
        }

        public void DiscoverMods( string basePath ) => DiscoverMods( new DirectoryInfo(basePath) );


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

//            FileSystemWatcherPasta();

            Mods = new ModCollection( basePath );
            Mods.Load();

            CalculateEffectiveFileList();
        }

        public static bool CalculateEffectiveFileList(ResolvedFiles resolvedFiles, SwappedFiles swappedFiles, IEnumerable<(ResourceMod mod, ModSettings settings)> enabledMods)
        {
            var changedConfig = false;
            resolvedFiles.Clear();
            swappedFiles.Clear();
            var registeredFiles = new Dictionary<GamePath, string>();

            foreach (var (mod, settings) in enabledMods)
            {
                mod.FileConflicts?.Clear();

                if(settings.Options == null) 
                {
                    settings.Options = new();
                    changedConfig = true;
                }

                foreach( var file in mod.ModFiles )
                {
                    RelPath relativeFilePath = new(file, mod.ModBasePath);
                    
                    var (configChanged, gamePaths) = mod.Meta.GetFilesForConfig(relativeFilePath, settings);
                    changedConfig = configChanged;

                    foreach (var gamePath in gamePaths)
                    {
                        if( !resolvedFiles.ContainsKey( gamePath ) )
                        {
                            resolvedFiles[gamePath] = file;
                            registeredFiles[gamePath] = mod.Meta.Name;
                        }
                        else if( registeredFiles.TryGetValue( gamePath, out var modName ) )
                        {
                            mod.AddConflict( modName, gamePath );
                        }
                    }
                }

                foreach( var swap in mod.Meta.FileSwaps )
                {
                    // just assume people put not fucked paths in here lol
                    if( !swappedFiles.ContainsKey( swap.Value ) )
                    {
                        swappedFiles[ swap.Key ] = swap.Value;
                        registeredFiles[ swap.Key ] = mod.Meta.Name;
                    }
                    else if( registeredFiles.TryGetValue( swap.Key, out var modName ) )
                    {
                        mod.AddConflict( modName, swap.Key );
                    }
                }
            }
            return changedConfig;
        }

        public void CalculateEffectiveFileList()
        {
            var enumerator = Mods.GetOrderedAndEnabledModListWithSettings( _plugin.Configuration.InvertModListOrder );
            if (CalculateEffectiveFileList(DefaultResolvedFiles, DefaultSwappedFiles, enumerator.Select( p => (p.Item1, p.Item2 as ModSettings))))
                _plugin.ModManager.Mods.Save();
            CharacterSettings.RenewFiles(Mods.ModSettings);
            _plugin.GameUtils.ReloadPlayerResources();
        }

        public void ChangeModPriority( ModInfo info, bool up = false )
        {
            Mods.ReorderMod( info, up );
            CalculateEffectiveFileList();
        }

        public void DeleteMod( ResourceMod mod )
        {
            if (mod?.ModBasePath?.Exists ?? false)
            {
                try
                {
                    Directory.Delete(mod.ModBasePath.FullName, true);
                }
                catch( Exception )
                {
                    // Todo: something sensible here.
                }
            }
            DiscoverMods();
        }

        public FileInfo GetCandidateForGameFile( GamePath gameResourcePath )
        {
            var val = CurrentResolvedFiles.TryGetValue( gameResourcePath, out var candidate );
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
            return CurrentSwappedFiles.TryGetValue( gameResourcePath, out var swappedPath ) ? swappedPath : GamePath.GenerateUnchecked(null);
        }

        public string ResolveSwappedOrReplacementFilePath( GamePath gameResourcePath )
        {
            return GetCandidateForGameFile( gameResourcePath )?.FullName ?? GetSwappedFilePath( gameResourcePath );
        }


        public void Dispose()
        {
            // _fileSystemWatcher?.Dispose();
        }

//        public void FileSystemWatcherPasta()
//        {
//             haha spaghet
//             _fileSystemWatcher?.Dispose();
//             _fileSystemWatcher = new FileSystemWatcher( _basePath.FullName )
//             {
//                 NotifyFilter = NotifyFilters.LastWrite |
//                                NotifyFilters.FileName |
//                                NotifyFilters.DirectoryName,
//                 IncludeSubdirectories = true,
//                 EnableRaisingEvents = true
//             };
//            
//             _fileSystemWatcher.Changed += FileSystemWatcherOnChanged;
//             _fileSystemWatcher.Created += FileSystemWatcherOnChanged;
//             _fileSystemWatcher.Deleted += FileSystemWatcherOnChanged;
//             _fileSystemWatcher.Renamed += FileSystemWatcherOnChanged;
//        }

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
    }
}
