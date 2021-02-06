using Newtonsoft.Json;
using System.Collections.Generic;
using Penumbra.Mods;
using System;

namespace Penumbra.Models
{
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
    }

    public class ModInfo : ModSettings
    {
        public bool Enabled { get; set; }
        public string FolderName { get; set; }

        [JsonIgnore]
        public ResourceMod Mod { get; set; }
    }
}