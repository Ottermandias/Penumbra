using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ModCollection
    {
        private readonly DirectoryInfo _basePath;

        public List< ModInfo > ModSettings { get; set; }
        public ResourceMod[] EnabledMods { get; set; }


        public ModCollection( DirectoryInfo basePath )
        {
            _basePath = basePath;
        }

        [Conditional("DEBUG")]
        private void PrintDebugInfo()
        {
            foreach( var ms in ModSettings )
            {
                PluginLog.Information( $"mod: {ms.FolderName} Enabled: {ms.Enabled} Priority: {ms.Priority}");
            }
        }

        private void ReadCollectionJson(string fileName)
        {
            // find the collection json
            var collectionPath = Path.Combine( _basePath.FullName, fileName );
            if( File.Exists( collectionPath ) )
            {
                try
                {
                    ModSettings = JsonConvert.DeserializeObject< List< ModInfo > >( File.ReadAllText( collectionPath ) );
                    ModSettings = ModSettings.OrderBy( x => x.Priority ).ToList();
                }
                catch( Exception e )
                {
                    PluginLog.Error( $"failed to read log collection information, failed path: {collectionPath}, err: {e.Message}" );
                }
            }
            ModSettings ??= new();
        }

        private List<string> GatherMods()
        {
            var foundMods = new List< string >();

            foreach( var modDir in _basePath.EnumerateDirectories() )
            {
                var metaFile = modDir.EnumerateFiles().FirstOrDefault( f => f.Name == "meta.json" );

                if( metaFile == null )
                {
                    // Allow for folders collecting ttmps or similar without pumping error outside of DEBUG.
#if DEBUG
                    PluginLog.LogError( "mod meta is missing for resource mod: {ResourceModLocation}", modDir );
#endif
                    continue;
                }

                var meta = ModMeta.LoadFromFile(metaFile.FullName);

                var mod = new ResourceMod
                {
                    Meta = meta,
                    ModBasePath = modDir
                };

                var modEntry = FindOrCreateModSettings( mod );
                foundMods.Add( modDir.Name );
                mod.RefreshModFiles();
            }
            return foundMods;
        }

        public void Load( bool invertOrder = false )
        {
            ReadCollectionJson("collection.json");
            PrintDebugInfo();

            var foundMods = GatherMods();

            // remove any mods from the collection we didn't find
            if (ModSettings.RemoveAll( x => !foundMods.Any(fm => string.Equals( x.FolderName, fm, StringComparison.InvariantCultureIgnoreCase ))) > 0)
            {
                ModSettings.Sort( (x,y) => x.Priority - y.Priority );
                var p = 0;
                ModSettings.ForEach( ms => ms.Priority = p++);
            }

            // reorder the resourcemods list so we can just directly iterate
            EnabledMods = GetOrderedAndEnabledModList( invertOrder ).ToArray();

            // write the collection metadata back to disk
            Save();
        }

        public void Save()
        {
            var collectionPath = Path.Combine( _basePath.FullName, "collection.json" );

            try
            {
                var data = JsonConvert.SerializeObject( ModSettings.OrderBy( x => x.Priority ).ToList() );
                File.WriteAllText( collectionPath, data );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"failed to write log collection information, failed path: {collectionPath}, err: {e.Message}" );
            }
        }

        public void ReorderMod( ModInfo info, bool up )
        {
            // todo: certified fucked tier

            var prio = info.Priority;
            var swapPrio = up ? prio + 1 : prio - 1;
            var swapMeta = ModSettings.FirstOrDefault( x => x.Priority == swapPrio );

            if( swapMeta == null )
            {
                return;
            }

            info.Priority = swapPrio;
            swapMeta.Priority = prio;

            // reorder mods list
            ModSettings = ModSettings.OrderBy( x => x.Priority ).ToList();
            EnabledMods = GetOrderedAndEnabledModList().ToArray();

            // save new prios
            Save();
        }


        public ModInfo FindModSettings( string name )
        {
            var settings = ModSettings.FirstOrDefault(
                x => string.Equals( x.FolderName, name, StringComparison.InvariantCultureIgnoreCase )
            );
#if DEBUG
            PluginLog.Information( "finding mod {ModName} - found: {ModSettingsExist}", name, settings != null );
#endif
            return settings;
        }

        public ModInfo AddModSettings( ResourceMod mod )
        {
            var entry = new ModInfo
            {
                Priority = ModSettings.Count,
                FolderName = mod.ModBasePath.Name,
                Enabled = true,
                Mod = mod
            };

#if DEBUG
            PluginLog.Information( "creating mod settings {ModName}", entry.FolderName );
#endif

            ModSettings.Add( entry );
            return entry;
        }

        public ModInfo FindOrCreateModSettings( ResourceMod mod )
        {
            var settings = FindModSettings( mod.ModBasePath.Name );
            if( settings != null )
            {
                settings.Mod = mod;
                return settings;
            }

            return AddModSettings( mod );
        }

        public IEnumerable<ModInfo> GetOrderedAndEnabledModSettings( bool invertOrder = false )
        {
            var query = ModSettings
                .Where( x => x.Enabled );

            if( !invertOrder )
            {
                return query.OrderBy( x => x.Priority );
            }

            return query.OrderByDescending( x => x.Priority );
        }

        public IEnumerable<ResourceMod> GetOrderedAndEnabledModList( bool invertOrder = false )
        {
            return GetOrderedAndEnabledModSettings( invertOrder )
                .Select( x => x.Mod );
        }

        public IEnumerable<(ResourceMod, ModInfo)> GetOrderedAndEnabledModListWithSettings( bool invertOrder = false )
        {
            return GetOrderedAndEnabledModSettings( invertOrder )
                .Select( x => (x.Mod, x) );
        }
    }
}
