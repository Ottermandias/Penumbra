using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dalamud.Plugin;

namespace Penumbra.Models
{
    public class Deduplicator
    {
        private const string Duplicates = "Duplicates";
        private const string Required   = "Required";

        private readonly DirectoryInfo _baseDir;
        private readonly int           _baseDirLength;
        private readonly ModMeta       _mod;
        private          SHA256        _hasher;

        private readonly Dictionary< long, List< FileInfo > > _filesBySize = new();

        private ref SHA256 Sha()
        {
            _hasher ??= SHA256.Create();
            return ref _hasher;
        }

        private Deduplicator( DirectoryInfo baseDir, ModMeta mod )
        {
            _baseDir       = baseDir;
            _baseDirLength = baseDir.FullName.Length;
            _mod           = mod;

            BuildDict();
        }

        private void BuildDict()
        {
            foreach( var file in _baseDir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
            {
                var fileLength = file.Length;
                if( _filesBySize.TryGetValue( fileLength, out var files ) )
                {
                    files.Add( file );
                }
                else
                {
                    _filesBySize[ fileLength ] = new List< FileInfo >() { file };
                }
            }
        }

        public static void Run( DirectoryInfo baseDir, ModMeta mod )
        {
            var dedup = new Deduplicator( baseDir, mod );
            foreach( var pair in dedup._filesBySize.Where( pair => pair.Value.Count >= 2 ) )
            {
                if( pair.Value.Count == 2 )
                {
                    if( CompareFilesDirectly( pair.Value[ 0 ], pair.Value[ 1 ] ) )
                    {
                        dedup.ReplaceFile( pair.Value[ 0 ], pair.Value[ 1 ] );
                    }
                }
                else
                {
                    var deleted = Enumerable.Repeat( false, pair.Value.Count ).ToArray();
                    var hashes  = pair.Value.Select( dedup.ComputeHash ).ToArray();

                    for( var i = 0; i < pair.Value.Count; ++i )
                    {
                        if( deleted[ i ] )
                        {
                            continue;
                        }

                        for( var j = i + 1; j < pair.Value.Count; ++j )
                        {
                            if( deleted[ j ] || !CompareHashes( hashes[ i ], hashes[ j ] ) )
                            {
                                continue;
                            }

                            dedup.ReplaceFile( pair.Value[ i ], pair.Value[ j ] );
                            deleted[ j ] = true;
                        }
                    }
                }
            }

            ClearEmptySubDirectories( dedup._baseDir );
        }

        private static Option FindOrCreateDuplicates( ModMeta meta )
        {
            static Option RequiredOption() =>
                new()
                {
                    OptionName  = Required,
                    OptionDesc  = "",
                    OptionFiles = new Dictionary< string, HashSet< string > >()
                };

            if( !meta.Groups.ContainsKey( Duplicates ) )
            {
                InstallerInfo info = new()
                {
                    GroupName     = Duplicates,
                    SelectionType = SelectType.Single,
                    Options       = new List< Option >() { RequiredOption() }
                };
                meta.Groups.Add( Duplicates, info );
                return meta.Groups[ Duplicates ].Options[ 0 ];
            }

            var group = meta.Groups[ Duplicates ];
            var idx   = group.Options.FindIndex( O => O.OptionName == Required );
            if( idx < 0 )
            {
                idx = group.Options.Count;
                group.Options.Add( RequiredOption() );
            }

            return group.Options[ idx ];
        }

        private void ReplaceFile( FileInfo f1, FileInfo f2 )
        {
            var relName1 = f1.FullName.Substring( _baseDirLength ).TrimStart( '\\' );
            var relName2 = f2.FullName.Substring( _baseDirLength ).TrimStart( '\\' );

            var inOption1 = false;
            var inOption2 = false;
            foreach( var group in _mod.Groups.Select( g => g.Value.Options ) )
            {
                foreach( var option in group )
                {
                    if( option.OptionFiles.ContainsKey( relName1 ) )
                    {
                        inOption1 = true;
                    }

                    if( !option.OptionFiles.TryGetValue( relName2, out var values ) )
                    {
                        continue;
                    }

                    inOption2 = true;
                    foreach( var value in values )
                    {
                        option.AddFile( relName1, value );
                    }

                    option.OptionFiles.Remove( relName2 );
                }
            }

            if( !inOption1 || !inOption2 )
            {
                var option = FindOrCreateDuplicates( _mod );
                if( !inOption1 )
                {
                    option.AddFile( relName1, relName2.Replace( '\\', '/' ) );
                }

                if( !inOption2 )
                {
                    option.AddFile( relName1, relName1.Replace( '\\', '/' ) );
                }
            }

            PluginLog.Information( $"File {relName1} and {relName2} are identical. Deleting the second." );
            f2.Delete();
        }

        public static bool CompareFilesDirectly( FileInfo f1, FileInfo f2 )
            => File.ReadAllBytes( f1.FullName ).SequenceEqual( File.ReadAllBytes( f2.FullName ) );

        public static bool CompareHashes( byte[] f1, byte[] f2 )
            => StructuralComparisons.StructuralEqualityComparer.Equals( f1, f2 );

        public byte[] ComputeHash( FileInfo f )
        {
            var stream = File.OpenRead( f.FullName );
            var ret    = Sha().ComputeHash( stream );
            stream.Dispose();
            return ret;
        }

        // Does not delete the base directory itself even if it is completely empty at the end.
        public static void ClearEmptySubDirectories( DirectoryInfo baseDir )
        {
            foreach( var subDir in baseDir.GetDirectories() )
            {
                ClearEmptySubDirectories( subDir );
                if( subDir.GetFiles().Length == 0 && subDir.GetDirectories().Length == 0 )
                {
                    subDir.Delete();
                }
            }
        }

        public enum GroupType
        {
            Both   = 0,
            Single = 1,
            Multi  = 2
        };

        public static void RemoveFromGroups( ModMeta meta, string relPath, string gamePath, GroupType type = GroupType.Both,
            bool skipDuplicates = true )
        {
            if( meta.Groups == null )
            {
                return;
            }

            var enumerator = type == GroupType.Both
                ? meta.Groups.Values
                : type == GroupType.Single
                    ? meta.Groups.Values.Where( G => G.SelectionType == SelectType.Single )
                    : meta.Groups.Values.Where( G => G.SelectionType == SelectType.Multi );
            foreach( var group in enumerator )
            {
                var optionEnum = skipDuplicates
                    ? group.Options.Where( o => group.GroupName != Duplicates || o.OptionName != Required )
                    : group.Options;
                foreach( var option in optionEnum )
                {
                    if( option.OptionFiles.TryGetValue( relPath, out var gamePaths )
                        && gamePaths.Remove( gamePath ) && gamePaths.Count == 0 )
                    {
                        option.OptionFiles.Remove( relPath );
                    }
                }
            }
        }

        private static bool FileIsInAnyGroup( ModMeta meta, string relPath )
        {
            return meta.Groups.Values.SelectMany( group => group.Options )
                .Any( option => option.OptionFiles.ContainsKey( relPath ) );
        }

        public static bool MoveFile( ModMeta meta, string basePath, string oldRelPath, string newRelPath )
        {
            if( oldRelPath == newRelPath )
            {
                return true;
            }

            try
            {
                var newFullPath = Path.Combine( basePath, newRelPath );
                new FileInfo( newFullPath ).Directory.Create();
                File.Move( Path.Combine( basePath, oldRelPath ), Path.Combine( basePath, newRelPath ) );
            }
            catch( Exception )
            {
                return false;
            }

            if( meta.Groups == null )
            {
                return true;
            }

            foreach( var option in meta.Groups.Values.SelectMany( group => group.Options ) )
            {
                if( option.OptionFiles.TryGetValue( oldRelPath, out var gamePaths ) )
                {
                    option.OptionFiles.Add( newRelPath, gamePaths );
                    option.OptionFiles.Remove( oldRelPath );
                }
            }

            return true;
        }

        // Goes through all Single-Select options and checks if file links are in each of them.
        // If they are, it moves those files to the root folder and removes them from the groups (and puts them to duplicates, if necessary).
        public static void RemoveUnnecessaryEntries( DirectoryInfo baseDir, ModMeta meta )
        {
            var basePath = baseDir.FullName;
            foreach( var group in meta.Groups.Values.Where( G => G.SelectionType == SelectType.Single && G.GroupName != Duplicates ) )
            {
                var                         firstOption = true;
                HashSet< (string, string) > groupList   = new();
                foreach( var option in group.Options )
                {
                    HashSet< (string, string) > optionList = new();
                    foreach( var (file, gamePaths) in option.OptionFiles.Select( P => ( P.Key, P.Value ) ) )
                    {
                        optionList.UnionWith( gamePaths.Select( p => ( file, p ) ) );
                    }

                    if( firstOption )
                    {
                        groupList = optionList;
                    }
                    else
                    {
                        groupList.IntersectWith( optionList );
                    }

                    firstOption = false;
                }

                var newPath = new Dictionary< string, string >();
                foreach( var (path, gamePath) in groupList )
                {
                    var p = gamePath.Replace( '/', '\\' );
                    if( newPath.TryGetValue( path, out var usedGamePath ) )
                    {
                        var required = FindOrCreateDuplicates( meta );
                        required.AddFile( usedGamePath, gamePath );
                        required.AddFile( usedGamePath, p );
                        RemoveFromGroups( meta, p, gamePath, GroupType.Single, true );
                    }
                    else
                    {
                        if( MoveFile( meta, basePath, path, p ) )
                        {
                            newPath[ path ] = gamePath;
                            if( FileIsInAnyGroup( meta, p ) )
                            {
                                FindOrCreateDuplicates( meta ).AddFile( p, gamePath );
                            }

                            RemoveFromGroups( meta, p, gamePath, GroupType.Single, true );
                        }
                    }
                }
            }

            ClearEmptySubDirectories( baseDir );
        }
    }
}