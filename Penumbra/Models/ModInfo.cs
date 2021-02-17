using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Penumbra.Mods;

namespace Penumbra.Models
{
    public class ModSettingsNames
    {
        public int Priority { get; set; }
        public Dictionary< string, HashSet< string > > Settings { get; set; }

        public void AddFromModSettings( ModSettings s, ModMeta meta )
        {
            Priority = s.Priority;
            Settings = s.Settings.Keys.ToDictionary( K => K, K => new HashSet< string >() );
            if( meta == null )
            {
                return;
            }

            foreach( var kvp in Settings )
            {
                if( !meta.Groups.TryGetValue( kvp.Key, out var info ) )
                {
                    continue;
                }

                var setting = s.Settings[ kvp.Key ];
                if( info.SelectionType == SelectType.Single )
                {
                    var name = setting < info.Options.Count
                        ? info.Options[ setting ].OptionName
                        : info.Options[ 0 ].OptionName;
                    kvp.Value.Add( name );
                }
                else
                {
                    for( var i = 0; i < info.Options.Count; ++i )
                    {
                        if( ( ( setting >> i ) & 1 ) != 0 )
                        {
                            kvp.Value.Add( info.Options[ i ].OptionName );
                        }
                    }
                }
            }
        }
    }


    public class ModSettings
    {
        public int Priority { get; set; }
        public Dictionary< string, int > Settings { get; set; }


        // For backwards compatibility
        private Dictionary< string, int > Conf
        {
            set => Settings = value;
        }

        public bool Equals( ModSettings rhs )
        {
            if( rhs.Priority != Priority )
            {
                return false;
            }

            foreach( var kvp in Settings )
            {
                if( !rhs.Settings.TryGetValue( kvp.Key, out var val ) || val != kvp.Value )
                {
                    return false;
                }
            }

            return true;
        }

        public static ModSettings ReduceFrom( ModInfo M )
            => new() { Priority = M.Priority, Settings = new Dictionary< string, int >( M.Settings ) };

        public static ModSettings CreateFrom( ModSettingsNames n, ModMeta meta )
        {
            ModSettings ret = new()
            {
                Priority = n.Priority,
                Settings = n.Settings.Keys.ToDictionary( K => K, K => 0 )
            };

            if( meta == null )
            {
                return ret;
            }

            foreach( var kvp in n.Settings )
            {
                if( !meta.Groups.TryGetValue( kvp.Key, out var info ) )
                {
                    continue;
                }

                if( info.SelectionType == SelectType.Single )
                {
                    if( n.Settings[ kvp.Key ].Count == 0 )
                    {
                        ret.Settings[ kvp.Key ] = 0;
                    }
                    else
                    {
                        var idx = info.Options.FindIndex( O => O.OptionName == n.Settings[ kvp.Key ].Last() );
                        ret.Settings[ kvp.Key ] = idx < 0 ? 0 : idx;
                    }
                }
                else
                {
                    foreach( var idx in n.Settings[ kvp.Key ]
                        .Select( option => info.Options.FindIndex( O => O.OptionName == option ) )
                        .Where( idx => idx >= 0 ) )
                    {
                        ret.Settings[ kvp.Key ] |= 1 << idx;
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

        public bool FixSpecificSetting( string name )
        {
            if( !Mod.Meta.Groups.TryGetValue( name, out var group ) )
            {
                return Settings.Remove( name );
            }

            if( Settings.TryGetValue( name, out var oldSetting ) )
            {
                Settings[ name ] = group.SelectionType switch
                {
                    SelectType.Single => Math.Min( Math.Max(oldSetting, 0 ), group.Options.Count - 1 ),
                    SelectType.Multi  => Math.Min( Math.Max(oldSetting, 0 ), ( 1 << group.Options.Count ) - 1 ),
                    _                 => Settings[ group.GroupName ]
                };
                return oldSetting != Settings[ group.GroupName ];
            }

            Settings[ name ] = 0;
            return true;
        }

        public bool FixInvalidSettings()
        {
            if( ( Mod.Meta.Groups?.Count ?? 0 ) == 0 )
            {
                var ret = Settings != null;
                Settings = new Dictionary< string, int >();
                return ret;
            }

            var changed = Settings == null;
            Settings ??= new Dictionary< string, int >();

            if( ( Mod.Meta.Groups?.Count ?? 0 ) == 0 )
            {
                return changed;
            }

            return Settings.Keys.ToArray().Union( Mod.Meta.Groups.Keys )
                .Aggregate( changed, ( current, name ) => current | FixSpecificSetting( name ) );
        }
    }
}