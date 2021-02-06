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

        [JsonProperty("ModSettings")]
        public Dictionary<string, ModSettingsNames> ModSettingsJson = new();

        [JsonIgnore]
        public Dictionary<string, ModSettings> ModSettings = new();

        [JsonIgnore]
        public ResolvedFiles ResolvedFiles{ get; set; } = new();
        [JsonIgnore]
        public SwappedFiles SwappedFiles { get; set; } = new();

        public CharacterSettings Copy()
        {
            return new(){ Enabled = Enabled, InvertOrder = InvertOrder, ModSettingsJson = new(ModSettingsJson), ModSettings = new(ModSettings) };
        }

        public void RenewFiles(List<ModInfo> allMods)
        {
            ComputeModSettings(allMods);
            ModManager.CalculateEffectiveFileList(ResolvedFiles, SwappedFiles, GetOrderedAndEnabledModSettings(allMods));
        }

        public void ComputeModSettings(List<ModInfo> allMods)
        {
            ModSettings.Clear();
            foreach (var kvp in ModSettingsJson)
            {
                var meta = allMods.FirstOrDefault( M => M.Mod.Meta.Name == kvp.Key)?.Mod?.Meta;
                if (meta == null)
                    continue;
                ModSettings[kvp.Key] = Models.ModSettings.CreateFrom(kvp.Value, meta);
            }
        }

        public void UpdateModSettingsJson(List<ModInfo> allMods)
        {
            foreach (var kvp in ModSettings)
            {
                var meta = allMods.FirstOrDefault( M => M.Mod.Meta.Name == kvp.Key)?.Mod?.Meta;
                if (!ModSettingsJson.TryGetValue(kvp.Key, out var value))
                {
                    ModSettingsJson[kvp.Key] = new();
                    ModSettingsJson[kvp.Key].AddFromModSettings(kvp.Value, meta);
                }
                else
                    value.AddFromModSettings(kvp.Value, meta);
            }
            RenewFiles(allMods);
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
            settings.UpdateModSettingsJson(defaultSettings);
            return settings;
        }
    }
}