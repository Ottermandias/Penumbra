using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Penumbra.Game;
using Penumbra.Models;
using Penumbra.Util;
using Swan.Formatters;

namespace Penumbra.Mods
{
    public class ResourceMod
    {
        public ModMeta Meta { get; set; }

        public DirectoryInfo ModBasePath { get; set; }

        public List< FileInfo > ModFiles { get; } = new();

        public Dictionary< string, List< GamePath > > FileConflicts { get; } = new();

        public bool ContainsMetaFile { get; private set; }
        public bool ContainsUnreloadableFile { get; private set; }

        public HashSet< ObjectInfo > ChangedObjectInformation { get; } = new();

        private void CheckForUnreloadables()
        {
            ContainsUnreloadableFile = ChangedObjectInformation
                .Any( info => GamePathParser.IsSkinTexture( info )
                    || GamePathParser.IsTailTexture( info ) );
        }

        public void RefreshModFiles()
        {
            if( ModBasePath == null )
            {
                PluginLog.LogError( "no basepath has been set on {ResourceModName}", Meta.Name );
                return;
            }

            ModFiles.Clear();
            ContainsMetaFile = false;
            // we don't care about any _files_ in the root dir, but any folders should be a game folder/file combo
            foreach( var dir in ModBasePath.EnumerateDirectories() )
            {
                foreach( var file in dir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
                {
                    if( file.Extension.ToLowerInvariant() == ".meta" )
                    {
                        ContainsMetaFile = true;
                        var meta = new Importer.TexToolsMeta( File.ReadAllBytes( file.FullName ) );
                        File.WriteAllText( file.FullName.Replace( ".meta", ".ttm" ), JsonConvert.SerializeObject(meta, Formatting.Indented) );
                    }
                    else
                    {
                        ModFiles.Add( file );
                        ChangedObjectInformation.UnionWith(
                            Meta.GetAllPossiblePathsForFile( new RelPath( file, ModBasePath ) )
                                .Select( GamePathParser.GetFileInfo )
                        );
                    }
                }
            }
            CheckForUnreloadables();
        }

        public void AddConflict( string modName, GamePath path )
        {
            if( FileConflicts.TryGetValue( modName, out var arr ) )
            {
                if( !arr.Contains( path ) )
                {
                    arr.Add( path );
                }

                return;
            }

            FileConflicts[ modName ] = new List< GamePath > { path };
        }
    }
}