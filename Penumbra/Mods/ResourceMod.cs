using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Penumbra.GameFiles;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class ResourceMod
    {
        public ModMeta Meta { get; set; }

        public DirectoryInfo ModBasePath { get; set; }

        public List< FileInfo > ModFiles { get; } = new();

        public Dictionary< string, List<GamePath> > FileConflicts { get; } = new();

        public bool ContainsMetaFile { get; private set; } = false;
        public bool ContainsUnreloadableFile { get; private set; } = false;

        public HashSet<ObjectInfo> ChangedFileInformation { get; } = new();

        private void CheckForUnreloadables()
        {
            ContainsUnreloadableFile = false;
            foreach (var info in ChangedFileInformation)
            {
                if (GamePathParser.IsSkinTexture(info) || GamePathParser.IsTailTexture(info))
                {
                    ContainsUnreloadableFile = true;
                    break;
                }
            }
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
                    if (file.Extension.ToLowerInvariant() == ".meta")
                    {
                        ContainsMetaFile = true;
                    }
                    else
                    {
                        ModFiles.Add( file );
                        ChangedFileInformation.UnionWith(Meta.GetAllPossiblePathsForFile(new(file, ModBasePath))
                                                             .Select( P => GamePathParser.GetFileInfo(P) ));
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
                    arr.Add( path );

                return;
            }

            FileConflicts[ modName ] = new(){ path };
        }
    }
}