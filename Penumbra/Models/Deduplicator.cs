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
            RelPath relPath1 = new(f1, _baseDir);
            RelPath relPath2 = new(f2, _baseDir);

            var inOption1 = false;
            var inOption2 = false;
            foreach (var group in _mod.Groups.Select( g => g.Value.Options))
            {
                foreach (var option in group)
                {
                    if (option.OptionFiles.ContainsKey(relPath1))
                        inOption1 = true;
                    if (option.OptionFiles.TryGetValue(relPath2, out var values))
                    {
                        inOption2 = true;
                        foreach (var value in values)
                            option.AddFile(relPath1, value);
                        option.OptionFiles.Remove(relPath2);
                    }
                }
            }

            if (!inOption1 || !inOption2)
            {
                var option = FindOrCreateDuplicates(_mod);
                if (!inOption1) option.AddFile(relPath1, new GamePath(relPath1));
                if (!inOption2) option.AddFile(relPath1, new GamePath(relPath2));
            }
           
            PluginLog.Information($"File {relPath1} and {relPath2} are identical. Deleting the second.");
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
        public static void RemoveFromGroups(ModMeta meta, RelPath relPath, GamePath gamePath, int singleOrMulti = 0, bool skipDuplicates = true)
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

        private static bool FileIsInAnyGroup(ModMeta meta, RelPath relPath, bool exceptDuplicates = false)
        {
            foreach (var group in meta.Groups.Values.Where( G => !exceptDuplicates || G.GroupName != Duplicates))
                foreach (var option in group.Options)
                    if (option.OptionFiles.ContainsKey(relPath))
                        return true;
            return false;
        }

        public static bool MoveFile(ModMeta meta, DirectoryInfo baseDir, RelPath oldRelPath, RelPath newRelPath)
        {
            if ((string) oldRelPath == newRelPath )
                return true;

            try
            {
                var newFullPath = Path.Combine(baseDir.FullName, newRelPath);
                new FileInfo(newFullPath).Directory.Create();
                File.Move(Path.Combine(baseDir.FullName, oldRelPath), Path.Combine(baseDir.FullName, newRelPath));
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
            foreach (var group in meta.Groups.Values.Where( G => G.SelectionType == SelectType.Single && G.GroupName != Duplicates ))
            {
                var firstOption = true;
                HashSet<(RelPath, GamePath)> groupList = new();
                foreach (var option in group.Options)
                {
                    HashSet<(RelPath, GamePath)> optionList = new();
                    foreach (var (file, gamePaths) in option.OptionFiles.Select( P => (P.Key, P.Value)))
                        optionList.UnionWith(gamePaths.Select(p => (file, p)));

                    if (firstOption)
                        groupList = optionList;
                    else
                        groupList.IntersectWith(optionList);
                    firstOption = false;
                }
                var newPath = new Dictionary<RelPath, GamePath>();
                foreach (var (path, gamePath) in groupList)
                {
                    RelPath p = new(gamePath);
                    if (newPath.TryGetValue(path, out var usedGamePath))
                    {
                        var required = FindOrCreateDuplicates(meta);
                        RelPath usedRelPath = new(usedGamePath);
                        required.AddFile(usedRelPath, gamePath);
                        required.AddFile(usedRelPath, usedGamePath);
                        RemoveFromGroups(meta, p, gamePath, 1, true);
                    }
                    else
                    {
                        if (MoveFile(meta, baseDir, path, p))
                        {
                            newPath[path] = gamePath;
                            if (FileIsInAnyGroup(meta, p))
                                FindOrCreateDuplicates(meta).AddFile(p, gamePath);
                            RemoveFromGroups(meta, p, gamePath, 1, true);
                        }
                    }
                }
            }

            // Clean up duplicates.
            if (meta.Groups.TryGetValue(Duplicates, out var info))
            {
                var requiredIdx = info.Options.FindIndex(O => O.OptionName == Required);
                if ( requiredIdx >= 0)
                {
                    var required = info.Options[requiredIdx];
                    foreach (var kvp in required.OptionFiles.ToArray())
                    { 
                        if (kvp.Value.Count > 1)
                            continue;

                        if (FileIsInAnyGroup(meta, kvp.Key, true))
                            continue;
                        if (kvp.Value.Count == 0 || (string) kvp.Value.ElementAt(0) == new GamePath(kvp.Key))
                            required.OptionFiles.Remove(kvp.Key);
                    }
                }
            }

            ClearEmptySubDirectories(baseDir);
        }
    }
}