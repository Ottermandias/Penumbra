using System;
using System.IO;

namespace Penumbra.Util
{
    public readonly struct RelPath : IComparable
    {
        public const int MaxRelPathLength = 256;

        private readonly string _path;

        private RelPath( string path, bool _ )
            => _path = path;

        private RelPath( string path )
        {
            if( path != null && path.Length < MaxRelPathLength )
            {
                _path = Trim( ReplaceSlash( path ) );
            }
            else
            {
                _path = null;
            }
        }

        public RelPath( FileInfo file, DirectoryInfo baseDir )
            => _path = CheckPre( file, baseDir ) ? Trim( Substring( file, baseDir ) ) : null;

        public RelPath( GamePath gamePath )
            => _path = gamePath ? ReplaceSlash( gamePath ) : null;

        private static bool CheckPre( FileInfo file, DirectoryInfo baseDir )
            => file.FullName.StartsWith( baseDir.FullName ) && file.FullName.Length < MaxRelPathLength;

        private static string Substring( FileInfo file, DirectoryInfo baseDir )
            => file.FullName.Substring( baseDir.FullName.Length );

        private static string ReplaceSlash( string path )
            => path.Replace( '/', '\\' );

        private static string Trim( string path )
            => path.TrimStart( '\\' );

        public static implicit operator bool( RelPath relPath )
            => relPath._path != null;

        public static implicit operator string( RelPath relPath )
            => relPath._path;

        public static explicit operator RelPath( string relPath )
            => new( relPath );

        public int CompareTo( object rhs )
        {
            return rhs switch
            {
                string       => string.Compare( _path, _path, StringComparison.InvariantCulture ),
                RelPath path => string.Compare( _path, path._path, StringComparison.InvariantCulture ),
                _            => -1
            };
        }

        public override string ToString()
            => _path;
    }

    public readonly struct GamePath : IComparable
    {
        public const int MaxGamePathLength = 256;

        private readonly string _path;

        private GamePath( string path, bool _ )
            => _path = path;

        public GamePath( string path )
        {
            if( path != null && path.Length < MaxGamePathLength )
            {
                _path = Lower( Trim( ReplaceSlash( path ) ) );
            }
            else
            {
                _path = null;
            }
        }

        public GamePath( FileInfo file, DirectoryInfo baseDir )
            => _path = CheckPre( file, baseDir ) ? Lower( Trim( ReplaceSlash( Substring( file, baseDir ) ) ) ) : null;

        public GamePath( RelPath relPath )
            => _path = relPath ? Lower( ReplaceSlash( relPath ) ) : null;

        private static bool CheckPre( FileInfo file, DirectoryInfo baseDir )
            => file.FullName.StartsWith( baseDir.FullName ) && file.FullName.Length < MaxGamePathLength;

        private static string Substring( FileInfo file, DirectoryInfo baseDir )
            => file.FullName.Substring( baseDir.FullName.Length );

        private static string ReplaceSlash( string path )
            => path.Replace( '\\', '/' );

        private static string Trim( string path )
            => path.TrimStart( '/' );

        private static string Lower( string path )
            => path.ToLowerInvariant();

        public static GamePath GenerateUnchecked( string path )
            => new( path, true );

        public static implicit operator bool( GamePath gamePath )
            => gamePath._path != null;

        public static implicit operator string( GamePath gamePath )
            => gamePath._path;

        public static explicit operator GamePath( string gamePath )
            => new( gamePath );


        public int CompareTo( object rhs )
        {
            return rhs switch
            {
                string        => string.Compare( _path, _path, StringComparison.InvariantCulture ),
                GamePath path => string.Compare( _path, path._path, StringComparison.InvariantCulture ),
                _             => -1
            };
        }

        public override string ToString()
            => _path;
    }
}