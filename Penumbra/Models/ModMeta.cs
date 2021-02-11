using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.IO;
using System;

namespace Penumbra.Models
{
    public class ModMeta
    {
        public uint FileVersion { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }

        public string Version { get; set; }

        public string Website { get; set; }

        public List< string > ChangedItems { get; set; } = new();

        public Dictionary<GamePath, GamePath> FileSwaps { get; } = new();

        public Dictionary<string, InstallerInfo> Groups { get; set; } = new();

        [JsonIgnore]
        public bool HasGroupWithConfig { get; set; } = false;

        public static ModMeta LoadFromFile(string filePath)
        {
            try
            {
                var meta = JsonConvert.DeserializeObject< ModMeta >( File.ReadAllText( filePath ));
                meta.HasGroupWithConfig = meta.Groups != null && meta.Groups.Count > 0 
                    && meta.Groups.Values.Any( G => G.SelectionType == SelectType.Multi || G.Options.Count > 1);
                return meta;
            }
            catch( Exception )
            {
                return new(){ Name = filePath };
                // todo: handle broken mods properly
            }
        }

        private static bool FixMaximalAvailableOptions(InstallerInfo group, ModSettings settings, out int selection)
        {
            if (!settings.Options.TryGetValue(group.GroupName, out selection) 
                || (group.SelectionType == SelectType.Single && settings.Options[group.GroupName] >= group.Options.Count))
            {
                settings.Options[group.GroupName] = 0;
                selection = 0;
                return true;
            }

            if (group.SelectionType == SelectType.Multi)
            {
                var newSetting = settings.Options[group.GroupName] & ((1 << group.Options.Count) - 1);
                if (newSetting != settings.Options[group.GroupName])
                {
                    settings.Options[group.GroupName] = newSetting;
                    return true;
                }
            }
            return false;
        }

        private static bool ApplySingleGroupFiles(InstallerInfo group, RelPath relPath, int selection, HashSet<GamePath> paths)
        {
            if (group.Options[selection].OptionFiles.TryGetValue(relPath, out var groupPaths))
            {
                paths.UnionWith(groupPaths);
                return true;
            }
            else
            {
                for(var i = 0; i < group.Options.Count; ++i)
                {
                    if (i == selection)
                        continue;
                    if(group.Options[i].OptionFiles.ContainsKey(relPath))
                        return true;
                }
            }
            return false;
        }

        private static bool ApplyMultiGroupFiles(InstallerInfo group, RelPath relPath, int selection, HashSet<GamePath> paths)
        {
            var doNotAdd = false;
            for(var i = 0; i < group.Options.Count; ++i)
            {
                if ((selection & (1 << i)) != 0)
                {
                    if (group.Options[i].OptionFiles.TryGetValue(relPath, out var groupPaths))
                        paths.UnionWith(groupPaths);
                }
                else if (group.Options[i].OptionFiles.ContainsKey(relPath))
                    doNotAdd = true;
            }
            return doNotAdd;
        }

        public IEnumerable<GamePath> GetAllPossiblePathsForFile(RelPath relPath)
        {
            return Groups.Values
                .SelectMany( G => G.Options )
                .Select( O => { if (O.OptionFiles.TryGetValue(relPath, out var paths)) return paths; return new HashSet<GamePath>(); } )
                .SelectMany( P => P )
                .DefaultIfEmpty(new GamePath(relPath));
        }

        public (bool configChanged, HashSet<GamePath> paths) GetFilesForConfig(RelPath relPath, ModSettings settings)
        {
            var doNotAdd = false;
            var configChanged = false;

            HashSet<GamePath> paths = new();
            foreach (var group in Groups.Values)
            {
                configChanged |= FixMaximalAvailableOptions(group, settings, out var selection);

                if (group.Options.Count == 0)
                    continue;
                        
                switch(group.SelectionType)
                {
                    case SelectType.Single:
                        doNotAdd |= ApplySingleGroupFiles(group, relPath, selection, paths);
                        break;
                    case SelectType.Multi:
                        doNotAdd |= ApplyMultiGroupFiles(group, relPath, selection, paths);
                        break;
                }
            }

            if (!doNotAdd)
                paths.Add( new(relPath) );

            return (configChanged, paths);
        }
    }
}