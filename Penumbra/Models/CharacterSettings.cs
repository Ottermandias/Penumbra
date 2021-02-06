using Newtonsoft.Json;
using System.Collections.Generic;
using Penumbra.Mods;
using System.Linq;
using System.IO;

namespace Penumbra.Models
{
    public class CharacterSettings
    {
        public bool Enabled { get; set; } = true;
        public bool InvertOrder { get; set; } = false;
        public Dictionary<string, ModSettings> ModSettings = new();

        [JsonIgnore]
        public ResolvedFiles ResolvedFiles{ get; set; } = new();
        [JsonIgnore]
        public SwappedFiles SwappedFiles { get; set; } = new();

        public void RenewFiles(List<ModInfo> allMods)
        {
            ModManager.CalculateEffectiveFileList(ResolvedFiles, SwappedFiles, GetOrderedAndEnabledModSettings(allMods));
        }

        public IEnumerable<(ResourceMod, ModSettings)> GetOrderedAndEnabledModSettings(List<ModInfo> allMods)
        {
            return allMods.Select( info => ModSettings.TryGetValue(info.Mod.Meta.Name, out var setting) ? (info, setting) : (info, null) )
                          .Where( p => p.setting != null )
                          .OrderBy( p => InvertOrder ? -p.setting.Priority : p.setting.Priority)
                          .Select( p => (p.info.Mod, p.setting) );
        }

        public static CharacterSettings ConvertFromDefault(bool invertOrder, List<ModInfo> defaultSettings)
        {
            CharacterSettings settings = new(){ InvertOrder = invertOrder };
            foreach (var mod in defaultSettings)
            {
                if (!mod.Enabled)
                    continue;

                settings.ModSettings[mod.Mod.Meta.Name] = Models.ModSettings.ReduceFrom(mod);
            }
            return settings;
        }
    }
}