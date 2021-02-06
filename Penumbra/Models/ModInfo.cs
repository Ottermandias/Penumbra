using Newtonsoft.Json;
using System.Collections.Generic;
using Penumbra.Mods;
using System;
using System.Linq;
using ImGuiScene;

namespace Penumbra.Models
{
    public class ModSettingsNames
    {
        public int Priority { get; set; }
        public Dictionary<string, HashSet<string>> Options { get; set; }

        public void AddFromModSettings(ModSettings s, ModMeta meta)
        {
            Priority = s.Priority;
            Options = s.Options.Keys.ToDictionary(K => K, K => new HashSet<string>());
            if (meta == null)
                return;

            foreach (var kvp in Options)
            {
                if (meta.Groups.TryGetValue(kvp.Key, out var info))
                {
                    var setting = s.Options[kvp.Key];
                    if (info.SelectionType == SelectType.Single)
                    {
                        if (setting < info.Options.Count)
                            kvp.Value.Add(info.Options[setting].OptionName);
                        else
                            kvp.Value.Add(info.Options[0].OptionName);
                    }
                    else
                    {
                        for(var i = 0; i < info.Options.Count; ++i)
                            if (((setting >> i) & 1) != 0)
                                kvp.Value.Add(info.Options[i].OptionName);
                    }
                }
            }
        }
    }

    public class ModSettings
    {
        public int Priority { get; set; }
        public Dictionary<string, int> Options { get; set; }

        public bool Equals(ModSettings rhs)
        { 
            if (rhs.Priority != Priority)
                return false;
            foreach (var kvp in Options)
            {
                if (!rhs.Options.TryGetValue(kvp.Key, out var val))
                    return false;
                if (val != kvp.Value)
                    return false;
            }
            return true;
        }

        public static ModSettings ReduceFrom(ModInfo M)
        {
            return new(){ Priority = M.Priority, Options = new(M.Options) };
        }

        public static ModSettings CreateFrom(ModSettingsNames n, ModMeta meta)
        {
            ModSettings ret = new();
            ret.Priority = n.Priority;
            ret.Options = n.Options.Keys.ToDictionary(K => K, K => 0);
            if (meta == null)
                return ret;

            foreach (var kvp in n.Options)
            {
                if (meta.Groups.TryGetValue(kvp.Key, out var info))
                {
                    if (info.SelectionType == SelectType.Single)
                    {
                        if (n.Options[kvp.Key].Count == 0)
                            ret.Options[kvp.Key] = 0;
                        else
                        {
                            var idx = info.Options.FindIndex( O => O.OptionName == n.Options[kvp.Key].Last());
                            ret.Options[kvp.Key] = idx < 0 ? 0 : idx;
                        }
                    }
                    else
                    {
                        foreach (var option in n.Options[kvp.Key])
                        {
                            var idx = info.Options.FindIndex( O => O.OptionName == option);
                            if (idx >= 0)
                                ret.Options[kvp.Key] |= (1 << idx);
                        }
                    }
                }
            }
            return ret;
        }
    }

    public class ModInfo : ModSettings
    {
        public bool Enabled { get; set; }
        public string FolderName { get; set; }

        [JsonIgnore]
        public ResourceMod Mod { get; set; }
    }
}