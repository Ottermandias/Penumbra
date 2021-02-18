using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Penumbra.Util;

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

        public Dictionary< GamePath, GamePath > FileSwaps { get; } = new();

        public Dictionary< string, InstallerInfo > Groups { get; set; } = new();

        [JsonIgnore]
        public bool HasGroupWithConfig { get; set; } = false;

        public IEnumerable< GamePath > GetAllPossiblePathsForFile( RelPath relPath )
        {
            return Groups.Values
                .SelectMany( G => G.Options )
                .Select( O => O.OptionFiles.TryGetValue( relPath, out var paths ) ? paths : new HashSet< GamePath >() )
                .SelectMany( P => P )
                .DefaultIfEmpty( new GamePath( relPath ) );
        }

        public static ModMeta LoadFromFile( string filePath )
        {
            try
            {
                var meta = JsonConvert.DeserializeObject< ModMeta >( File.ReadAllText( filePath ) );
                meta.HasGroupWithConfig =
                    meta.Groups != null
                    && meta.Groups.Count > 0
                    && meta.Groups.Values.Any( G => G.SelectionType == SelectType.Multi || G.Options.Count > 1 );

                return meta;
            }
            catch( Exception )
            {
                return new ModMeta { Name = filePath };
                // todo: handle broken mods properly
            }
        }
    }
}