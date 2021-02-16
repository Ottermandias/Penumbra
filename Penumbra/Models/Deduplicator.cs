using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Dalamud.Plugin;
using Penumbra.Util;

namespace Penumbra.Models
{
    public class Deduplicator
    {
        private const string Duplicates = "Duplicates";
        private const string Required   = "Required";

        private readonly DirectoryInfo _baseDir;
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
            _baseDir = baseDir;
            _mod     = mod;
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
                    OptionFiles = new Dictionary< RelPath, HashSet< GamePath > >()
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
            RelPath relName1 = new( f1, _baseDir );
            RelPath relName2 = new( f2, _baseDir );

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
                    option.AddFile( relName1, new GamePath( relName2 ) );
                }

                if( !inOption2 )
                {
                    option.AddFile( relName1, new GamePath( relName1 ) );
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

        public static void RemoveFromGroups( ModMeta meta, RelPath relPath, GamePath gamePath
            , GroupType type = GroupType.Both, bool skipDuplicates = true )
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

        private static bool FileIsInAnyGroup( ModMeta meta, RelPath relPath )
        {
            return meta.Groups.Values.SelectMany( group => group.Options )
                .Any( option => option.OptionFiles.ContainsKey( relPath ) );
        }

        public static bool MoveFile( ModMeta meta, DirectoryInfo basePath, RelPath oldRelPath, RelPath newRelPath )
        {
            if( oldRelPath.CompareTo( newRelPath ) == 0 )
            {
                return true;
            }

            try
            {
                var newFullPath = Path.Combine( basePath.FullName, newRelPath );
                new FileInfo( newFullPath ).Directory?.Create();
                File.Move( Path.Combine( basePath.FullName, oldRelPath ), Path.Combine( basePath.FullName, newRelPath ) );
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
            foreach( var group in meta.Groups.Values.Where( G => G.SelectionType == SelectType.Single && G.GroupName != Duplicates ) )
            {
                var firstOption = true;

                HashSet< (RelPath, GamePath) > groupList  = new();
                HashSet< (RelPath, GamePath) > optionList = new();
                foreach( var option in group.Options )
                {
                    optionList.Clear();
                    foreach( var (file, gamePaths) in option.OptionFiles.Select( P => ( P.Key, P.Value ) ) )
                    {
                        optionList.UnionWith( gamePaths.Select( p => ( file, p ) ) );
                    }

                    if( firstOption )
                    {
                        groupList.UnionWith( optionList );
                    }
                    else
                    {
                        groupList.IntersectWith( optionList );
                    }

                    firstOption = false;
                }

                var newPath = new Dictionary< RelPath, GamePath >();
                foreach( var (path, gamePath) in groupList )
                {
                    RelPath p = new( gamePath );
                    if( newPath.TryGetValue( path, out var usedGamePath ) )
                    {
                        var     required    = FindOrCreateDuplicates( meta );
                        RelPath usedRelPath = new( usedGamePath );
                        required.AddFile( usedRelPath, gamePath );
                        required.AddFile( usedRelPath, usedGamePath );
                        RemoveFromGroups( meta, p, gamePath, GroupType.Single, true );
                    }
                    else if( MoveFile( meta, baseDir, path, p ) )
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

            ClearEmptySubDirectories( baseDir );
        }
    }
}