using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Collections;
using Dalamud.Plugin;
using System;

namespace Penumbra.Models
{
    public class Deduplicator
    {
        private const string Duplicates = "Duplicates";
        private const string Required   = "Required";

        private readonly DirectoryInfo _baseDir;
        private readonly string        _basePath;
        private readonly int           _baseDirLength;
        private readonly ModMeta       _mod;
        private SHA256                 _hasher = null;

        private Dictionary<long, List<FileInfo>> filesBySize;

        private ref SHA256 Sha()
        {
            if (_hasher == null)
                _hasher = SHA256.Create();
            return ref _hasher;
        }

        private Deduplicator(DirectoryInfo baseDir, ModMeta mod)
        {
            this._baseDir       = baseDir;
            this._basePath      = baseDir.FullName;
            this._baseDirLength = _basePath.Length;
            this._mod           = mod;
            filesBySize        = new();

            BuildDict();  
        }

        private void BuildDict()
        {
            foreach( var file in _baseDir.EnumerateFiles( "*.*", SearchOption.AllDirectories ) )
            {
                var fileLength = file.Length;
                if (filesBySize.TryGetValue(fileLength, out var files))
                    files.Add(file);
                else
                    filesBySize[fileLength] = new(){ file };
            }
        }

        public static void Run(DirectoryInfo baseDir, ModMeta mod)
        {
            var dedup = new Deduplicator(baseDir, mod);

            foreach (var pair in dedup.filesBySize)
            {
                if (pair.Value.Count < 2)
                    continue;

                if (pair.Value.Count == 2)
                {
                    if (CompareFilesDirectly(pair.Value[0], pair.Value[1]))
                        dedup.ReplaceFile(pair.Value[0], pair.Value[1]);
                }
                else
                {
                    var deleted = Enumerable.Repeat(false, pair.Value.Count).ToArray();
                    var hashes  = pair.Value.Select( F => dedup.ComputeHash(F)).ToArray();

                    for (var i = 0; i < pair.Value.Count; ++i)
                    {
                        if (deleted[i])
                            continue;

                        for (var j = i + 1; j < pair.Value.Count; ++j)
                        {
                            if (deleted[j])
                                continue;

                            if (!CompareHashes(hashes[i], hashes[j]))
                                continue;

                            dedup.ReplaceFile(pair.Value[i], pair.Value[j]);
                            deleted[j] = true;
                        }
                    }
                }
            }
            ClearEmptySubDirectories(dedup._baseDir);
        }

        private static Option FindOrCreateDuplicates(ModMeta meta)
        {
            Option RequiredOption()
            {
                return new()
                {
                    OptionName  = Required, 
                    OptionDesc  = "", 
                    OptionFiles = new()
                };
            }

            if (!meta.Groups.ContainsKey(Duplicates))
            {
                InstallerInfo info = new()
                {
                    GroupName = Duplicates,
                    SelectionType = SelectType.Single,
                    Options = new() { RequiredOption() }
                };
                meta.Groups.Add(Duplicates, info);
                return meta.Groups[Duplicates].Options[0];
            }
            var group = meta.Groups[Duplicates];
            var idx = group.Options.FindIndex( O => O.OptionName == Required );
            if (idx < 0)
            {
                idx = group.Options.Count;
                group.Options.Add( RequiredOption() );
            }
            return group.Options[idx];
        }

        private void ReplaceFile(FileInfo f1, FileInfo f2)
        {
            var relName1 = f1.FullName.Substring(_baseDirLength).TrimStart('\\');
            var relName2 = f2.FullName.Substring(_baseDirLength).TrimStart('\\');

            var inOption = false;
            foreach (var group in _mod.Groups.Select( g => g.Value.Options))
            {
                foreach (var option in group)
                {
                    if (option.OptionFiles.TryGetValue(relName2, out var values))
                    {
                        inOption = true;
                        foreach (var value in values)
                            option.AddFile(relName1, value);
                        option.OptionFiles.Remove(relName2);
                    }
                }
            }
            if (!inOption)
            {
                var option = FindOrCreateDuplicates(_mod);
                option.AddFile(relName1, relName2.Replace('\\', '/'));
                option.AddFile(relName1, relName1.Replace('\\', '/'));
            }
            PluginLog.Information($"File {relName1} and {relName2} are identical. Deleting the second.");
            f2.Delete();
        }

        public static bool CompareFilesDirectly(FileInfo f1, FileInfo f2)
        {
            return File.ReadAllBytes(f1.FullName).SequenceEqual(File.ReadAllBytes(f2.FullName));
        }

        public static bool CompareHashes(byte[] f1, byte[] f2)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(f1, f2);
        }

        public byte[] ComputeHash(FileInfo f)
        {
            var stream = File.OpenRead( f.FullName );
            var ret = Sha().ComputeHash(stream);
            stream.Dispose();
            return ret;
        }

        // Does not delete the base directory itself even if it is completely empty at the end.
        public static void ClearEmptySubDirectories(DirectoryInfo baseDir)
        {
            foreach (var subDir in baseDir.GetDirectories())
            {
                ClearEmptySubDirectories(subDir);
                if (subDir.GetFiles().Length == 0 && subDir.GetDirectories().Length == 0)
                    subDir.Delete();
            }
        }


        // singleOrMulti: 0 - Both, 1 - Single - else Multi
        public static void RemoveFromGroups(ModMeta meta, string relPath, string gamePath, int singleOrMulti = 0, bool skipDuplicates = true)
        {
            if (meta.Groups == null)
                return;
            var enumerator = singleOrMulti == 0
                            ? meta.Groups.Values : singleOrMulti == 1
                            ? meta.Groups.Values.Where( G => G.SelectionType == SelectType.Single)
                            : meta.Groups.Values.Where( G => G.SelectionType == SelectType.Multi);
            foreach (var group in enumerator)
            {
                foreach (var option in group.Options)
                {
                    if (skipDuplicates && group.GroupName == Duplicates && option.OptionName == Required)
                        continue;

                    if (option.OptionFiles.TryGetValue(relPath, out var gamePaths))
                    {
                        if (gamePaths.Remove(gamePath) && gamePaths.Count == 0)
                            option.OptionFiles.Remove(relPath);
                    }
                }
            }
        }

        public static bool MoveFile(ModMeta meta, string basePath, string oldRelPath, string newRelPath)
        {
            if (oldRelPath == newRelPath)
                return false;

            try
            {
                var newFullPath = Path.Combine(basePath, newRelPath);
                new FileInfo(newFullPath).Directory.Create();
                File.Move(Path.Combine(basePath, oldRelPath), Path.Combine(basePath, newRelPath));
            }
            catch (Exception)
            {
                return false;
            }
            if (meta.Groups == null)
                return true;

            foreach (var group in meta.Groups.Values)
            {
                foreach (var option in group.Options)
                {
                    if (option.OptionFiles.TryGetValue(oldRelPath, out var gamePaths))
                    {
                        option.OptionFiles.Add(newRelPath, gamePaths);
                        option.OptionFiles.Remove(oldRelPath);
                    }
                }
            }
            return true;
        }

        // Goes through all Single-Select options and checks if file links are in each of them.
        // If they are, it moves those files to the root folder and removes them from the groups (and puts them to duplicates, if necessary).
        public static void RemoveUnneccessaryEntries(DirectoryInfo baseDir, ModMeta meta)
        {
            var basePath = baseDir.FullName;
            foreach (var group in meta.Groups.Values.Where( G => G.SelectionType == SelectType.Single && G.GroupName != Duplicates ))
            {
                var firstOption = true;
                HashSet<(string, string)> groupList = new();
                foreach (var option in group.Options)
                {
                    HashSet<(string, string)> optionList = new();
                    foreach (var (file, gamePaths) in option.OptionFiles.Select( P => (P.Key, P.Value)))
                        optionList.UnionWith(gamePaths.Select(p => (file, p)));

                    if (firstOption)
                        groupList = optionList;
                    else
                        groupList.IntersectWith(optionList);
                    firstOption = false;
                }
                var newPath = new Dictionary<string, string>();
                foreach (var (path, gamePath) in groupList)
                {
                    if (newPath.TryGetValue(path, out var usedGamePath))
                    {
                        var required = FindOrCreateDuplicates(meta);
                        required.AddFile(usedGamePath, gamePath);
                        required.AddFile(usedGamePath, usedGamePath);
                        RemoveFromGroups(meta, usedGamePath, gamePath, 1, true);
                    }
                    else
                    {
                        if (MoveFile(meta, basePath, path, gamePath))
                        {
                            newPath[path] = gamePath;
                            RemoveFromGroups(meta, gamePath, gamePath, 1, true);
                        }
                    }
                }
            }
            ClearEmptySubDirectories(baseDir);
        }
    }
}