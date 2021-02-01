using System.IO;
using System;

public struct RelPath : IComparable
{
    public const int MaxRelPathLength = 256;

    private string _path;
    private RelPath(string path, bool _) => _path = path;
    private RelPath(string path)
    {
        if (path != null && path.Length < MaxRelPathLength)
            _path = Trim(ReplaceSlash(path));
        else
            _path = null;
    }

    public RelPath(FileInfo file, DirectoryInfo baseDir)
    {
        if (CheckPre(file, baseDir))
            _path = Trim(Substring(file, baseDir));
        else
            _path = null;
    }

    public RelPath(GamePath gamePath) => _path = gamePath ? ReplaceSlash(gamePath) : null;

    private static bool   CheckPre(FileInfo file, DirectoryInfo baseDir)  => file.FullName.StartsWith(baseDir.FullName) && file.FullName.Length < MaxRelPathLength;
    private static string Substring(FileInfo file, DirectoryInfo baseDir) => file.FullName.Substring(baseDir.FullName.Length);
    private static string ReplaceSlash(string path) => path.Replace('/', '\\');
    private static string Trim(string path)         => path.TrimStart('\\');

    public static implicit operator bool(RelPath relPath)   => relPath._path != null;
    public static implicit operator string(RelPath relPath) => relPath._path;
    public static explicit operator RelPath(string relPath) => new(relPath);

    public int CompareTo(object rhs)
    {
        if (rhs is string)
            return _path.CompareTo(rhs);
        if (rhs is RelPath)
        {
            var r = (RelPath) rhs;
            return _path.CompareTo(r._path);
        }
        return -1;
    }

    public override string ToString() => _path;
}

public struct GamePath : IComparable
{
    public const int MaxGamePathLength = 256;

    private string _path;

    private GamePath(string path, bool _) => _path = path;
    public GamePath(string path)
    {
        if (path != null && path.Length < MaxGamePathLength)
            _path = Lower(Trim(ReplaceSlash(path)));
        else
            _path = null;
    }

    public GamePath(FileInfo file, DirectoryInfo baseDir)
    {
        if (CheckPre(file, baseDir))
            _path = Lower(Trim(ReplaceSlash(Substring(file, baseDir))));
        else
            _path = null;
    }

    public GamePath(RelPath relPath) => _path = relPath ? Lower(ReplaceSlash(relPath)) : null;

    private static bool   CheckPre(FileInfo file, DirectoryInfo baseDir)  => file.FullName.StartsWith(baseDir.FullName) && file.FullName.Length < MaxGamePathLength;
    private static string Substring(FileInfo file, DirectoryInfo baseDir) => file.FullName.Substring(baseDir.FullName.Length);
    private static string ReplaceSlash(string path) => path.Replace('\\', '/');
    private static string Trim(string path)         => path.TrimStart('/');
    private static string Lower(string path)        => path.ToLowerInvariant();
    public static GamePath GenerateUnchecked(string path) => new(path, true);

    public static implicit operator bool(GamePath gamePath)   => gamePath._path != null;
    public static implicit operator string(GamePath GamePath) => GamePath._path;
    public static explicit operator GamePath(string GamePath) => new(GamePath);

    public int CompareTo(object rhs)
    {
        if (rhs is string)
            return _path.CompareTo(rhs);
        if (rhs is GamePath)
        {
            var r = (GamePath) rhs;
            return _path.CompareTo(r._path);
        }
        return -1;
    }

    public override string ToString() => _path;
}