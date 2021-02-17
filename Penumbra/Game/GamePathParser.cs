using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Dalamud.Plugin;
using Penumbra.Util;

namespace Penumbra.Game
{
    public static class GamePathParser
    {
        private const string CharacterFolder = "chara/";
        private const string EquipmentFolder = "equipment/";
        private const string PlayerFolder    = "human/";
        private const string WeaponFolder    = "weapon/";
        private const string AccessoryFolder = "accessory/";
        private const string DemiHumanFolder = "demihuman/";
        private const string MonsterFolder   = "monster/";
        private const string CommonFolder    = "common/";
        private const string UiFolder        = "ui/";
        private const string IconFolder      = "icon/";
        private const string LoadingFolder   = "loadingimage/";
        private const string MapFolder       = "map/";
        private const string InterfaceFolder = "uld/";
        private const string FontFolder      = "common/font/";
        private const string HousingFolder   = "bgcommon/hou/";
        private const string VfxFolder       = "vfx/";
        private const string WorldFolder1    = "bgcommon/";
        private const string WorldFolder2    = "bg/";

        // @formatter:off
        private static readonly Dictionary<FileType, Dictionary<ObjectType, Regex[]>> Regexes = new()
        { { FileType.Font, new Dictionary< ObjectType, Regex[] >(){ { ObjectType.Font, new Regex[]{ new(@"common/font/(?'fontname'.*)_(?'id'\d\d)(_lobby)?\.fdt") } } } }
        , { FileType.Texture, new Dictionary< ObjectType, Regex[] >()
            { { ObjectType.Icon,      new Regex[]{ new(@"ui/icon/(?'group'\d*)(/(?'lang'[a-z]{2}))?(/(?'hq'hq))?/(?'id'\d*)\.tex") } }
            , { ObjectType.Map,       new Regex[]{ new(@"ui/map/(?'id'[a-z0-9]{4})/(?'variant'\d{2})/\k'id'\k'variant'(?'suffix'[a-z])?(_[a-z])?\.tex")  } }
            , { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'weapon'\d{4})/obj/body/b(?'id'\d{4})/texture/v(?'variant'\d{2})_w\k'weapon'b\k'id'(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/texture/v(?'variant'\d{2})_m\k'monster'b\k'id'(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/texture/v(?'variant'\d{2})_c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.DemiHuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/texture/v(?'variant'\d{2})_d\k'id'e\k'equip'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/texture/v(?'variant'\d{2})_c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})_[a-z]\.tex") } }
            , { ObjectType.Character, new Regex[]{ new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/texture/(?'minus'(--)?)c\k'race'\k'typeabr'\k'id'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex")
                                                 , new(@"chara/common/texture/skin(?'skin'.*)\.tex")
                                                 , new(@"chara/common/texture/decal_(?'location'[a-z]+)/[-_]?decal_(?'id'\d+).tex") } } } }
        , { FileType.Model, new Dictionary< ObjectType, Regex[] >()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'weapon'\d{4})/obj/body/b(?'id'\d{4})/model/w\k'weapon'b\k'id'\.mdl") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/model/m\k'monster'b\k'id'\.mdl") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/model/c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.DemiHuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/model/d\k'id'e\k'equip'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/model/c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.Character, new Regex[]{ new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/model/c\k'race'\k'typeabr'\k'id'_(?'slot'[a-z]{3})\.mdl") } } } }
        , { FileType.Material, new Dictionary< ObjectType, Regex[] >()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'weapon'\d{4})/obj/body/b(?'id'\d{4})/material/v(?'variant'\d{4})/mt_w\k'weapon'b\k'id'_[a-z]\.mtrl") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/material/v(?'variant'\d{4})/mt_m\k'monster'b\k'id'_[a-z]\.mtrl") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.DemiHuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/material/v(?'variant'\d{4})/mt_d\k'id'e\k'equip'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.Character, new Regex[]{ new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c\k'race'\k'typeabr'\k'id'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } } } }
        , { FileType.Imc, new Dictionary< ObjectType, Regex[] >()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'weapon'\d{4})/obj/body/b(?'id'\d{4})/b\k'id'\.imc") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/b\k'id'\.imc") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/e\k'id'\.imc") } }
            , { ObjectType.DemiHuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/e\k'equip'\.imc") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/a\k'id'\.imc") } } } }
        };

        public static readonly Dictionary<string, FileType> ExtensionToType = new()
        { { ".mdl" , FileType.Model       }
        , { ".tex" , FileType.Texture     }
        , { ".mtrl", FileType.Material    }
        , { ".atex", FileType.Animation   }
        , { ".avfx", FileType.Vfx         }
        , { ".scd" , FileType.Sound       }
        , { ".imc" , FileType.Imc         }
        , { ".pap" , FileType.Pap         }
        , { ".eqp" , FileType.MetaInfo    }
        , { ".eqdp", FileType.MetaInfo    }
        , { ".est" , FileType.MetaInfo    }
        , { ".exd" , FileType.MetaInfo    }
        , { ".exh" , FileType.MetaInfo    }
        , { ".shpk", FileType.Shader      }
        , { ".shcd", FileType.Shader      }
        , { ".fdt" , FileType.Font        }
        , { ".envb", FileType.Environment }
        };

        public static readonly Dictionary<string, EquipSlot> SlotToEquip = new()
        { { "met", EquipSlot.Head   }
        , { "glv", EquipSlot.Hands  }
        , { "dwn", EquipSlot.Legs   }
        , { "sho", EquipSlot.Feet   }
        , { "top", EquipSlot.Body   }
        , { "ear", EquipSlot.Ears   }
        , { "nek", EquipSlot.Neck   }
        , { "rir", EquipSlot.RingR  }
        , { "ril", EquipSlot.RingL  }
        , { "wrs", EquipSlot.Wrists }
        };

        public static readonly Dictionary<string, Customization> SlotToCustomization = new()
        { { "fac", Customization.Face      }
        , { "iri", Customization.Iris      }
        , { "acc", Customization.Accessory }
        , { "hir", Customization.Hair      }
        , { "til", Customization.Tail      }
        , { "etc", Customization.Etc       }
        };

        public static readonly Dictionary<string, BodySlot> SlotToBodyslot = new()
        { { "zear", BodySlot.Zear }
        , { "face", BodySlot.Face }
        , { "hair", BodySlot.Hair }
        , { "body", BodySlot.Body }
        , { "tail", BodySlot.Tail }
        };


        public static readonly Dictionary<string, (Gender, Race)> IdToRace = new()
        { { "0101", (Gender.Male,      Race.Midlander)  }
        , { "0104", (Gender.MaleNpc,   Race.Midlander)  }
        , { "0201", (Gender.Female,    Race.Midlander)  }
        , { "0204", (Gender.FemaleNpc, Race.Midlander)  }
        , { "0301", (Gender.Male,      Race.Highlander) }
        , { "0304", (Gender.MaleNpc,   Race.Highlander) }
        , { "0401", (Gender.Female,    Race.Highlander) }
        , { "0404", (Gender.FemaleNpc, Race.Highlander) }
        , { "0501", (Gender.Male,      Race.Elezen)     }
        , { "0504", (Gender.MaleNpc,   Race.Elezen)     }
        , { "0601", (Gender.Female,    Race.Elezen)     }
        , { "0604", (Gender.FemaleNpc, Race.Elezen)     }
        , { "0701", (Gender.Male,      Race.Miqote)     }
        , { "0704", (Gender.MaleNpc,   Race.Miqote)     }
        , { "0801", (Gender.Female,    Race.Miqote)     }
        , { "0804", (Gender.FemaleNpc, Race.Miqote)     }
        , { "0901", (Gender.Male,      Race.Roegadyn)   }
        , { "0904", (Gender.MaleNpc,   Race.Roegadyn)   }
        , { "1001", (Gender.Female,    Race.Roegadyn)   }
        , { "1004", (Gender.FemaleNpc, Race.Roegadyn)   }
        , { "1101", (Gender.Male,      Race.Lalafell)   }
        , { "1104", (Gender.MaleNpc,   Race.Lalafell)   }
        , { "1201", (Gender.Female,    Race.Lalafell)   }
        , { "1204", (Gender.FemaleNpc, Race.Lalafell)   }
        , { "1301", (Gender.Male,      Race.AuRa)       }
        , { "1304", (Gender.MaleNpc,   Race.AuRa)       }
        , { "1401", (Gender.Female,    Race.AuRa)       }
        , { "1404", (Gender.FemaleNpc, Race.AuRa)       }
        , { "1501", (Gender.Male,      Race.Hrothgar)   }
        , { "1504", (Gender.MaleNpc,   Race.Hrothgar)   }
        , { "1801", (Gender.Female,    Race.Viera)      }
        , { "1804", (Gender.FemaleNpc, Race.Viera)      }
        , { "9104", (Gender.MaleNpc,   Race.Unknown)    }
        , { "9204", (Gender.FemaleNpc, Race.Unknown)    }
        };
        // @formatter:on

        public static ObjectType PathToObjectType( GamePath path )
        {
            if( !path )
            {
                return ObjectType.Unknown;
            }

            string p       = path;
            var    folders = p.Split( '/' );
            if( folders.Length < 2 )
            {
                return ObjectType.Unknown;
            }

            return folders[ 0 ] switch
            {
                CharacterFolder => folders[ 1 ] switch
                {
                    EquipmentFolder => ObjectType.Equipment,
                    AccessoryFolder => ObjectType.Accessory,
                    WeaponFolder    => ObjectType.Weapon,
                    PlayerFolder    => ObjectType.Character,
                    DemiHumanFolder => ObjectType.DemiHuman,
                    MonsterFolder   => ObjectType.Monster,
                    CommonFolder    => ObjectType.Character,
                    _               => ObjectType.Unknown
                },
                UiFolder => folders[ 1 ] switch
                {
                    IconFolder      => ObjectType.Icon,
                    LoadingFolder   => ObjectType.LoadingScreen,
                    MapFolder       => ObjectType.Map,
                    InterfaceFolder => ObjectType.Interface,
                    _               => ObjectType.Unknown
                },
                FontFolder    => ObjectType.Font,
                HousingFolder => ObjectType.Housing,
                WorldFolder1  => ObjectType.World,
                WorldFolder2  => ObjectType.World,
                VfxFolder     => ObjectType.Vfx,
                _             => ObjectType.Unknown
            };
        }

        private static (FileType, ObjectType, Match) ParseGamePath( GamePath path )
        {
            if( !ExtensionToType.TryGetValue( Extension( path ), out var fileType ) )
            {
                fileType = FileType.Unknown;
            }

            var objectType = PathToObjectType( path );

            if( !Regexes.TryGetValue( fileType, out var objectDict ) )
            {
                return ( fileType, objectType, null );
            }

            if( !objectDict.TryGetValue( objectType, out var regexes ) )
            {
                return ( fileType, objectType, null );
            }

            foreach( var regex in regexes )
            {
                var match = regex.Match( path );
                if( match.Success )
                {
                    return ( fileType, objectType, match );
                }
            }

            return ( fileType, objectType, null );
        }

        private static string Extension( string filename )
        {
            var extIdx = filename.LastIndexOf( '.' );
            return extIdx < 0 ? "" : filename.Substring( extIdx );
        }

        private static ObjectInfo HandleEquipment( FileType fileType, ObjectType objectType, GroupCollection groups )
        {
            try
            {
                var ret = new EquipInfo
                {
                    FileType   = fileType,
                    ObjectType = objectType,
                    ItemId     = ushort.Parse( groups[ "id" ].Value )
                };
                if( fileType == FileType.Imc )
                {
                    return ret;
                }

                var (gender, race) = IdToRace[ groups[ "race" ].Value ];
                ret.Gender         = gender;
                ret.Race           = race;
                ret.Slot           = SlotToEquip[ groups[ "slot" ].Value ];
                if( fileType == FileType.Model )
                {
                    return ret;
                }

                ret.Variant = ushort.Parse( groups[ "variant" ].Value );
                return ret;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Parsing game path failed:\n{e}" );
                return new ObjectInfo { FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleWeapon( FileType fileType, ObjectType objectType, GroupCollection groups )
        {
            try
            {
                var ret = new WeaponInfo
                {
                    FileType   = fileType,
                    ObjectType = objectType,
                    ItemId     = ushort.Parse( groups[ "weapon" ].Value ),
                    Set        = ushort.Parse( groups[ "id" ].Value )
                };
                if( fileType == FileType.Imc || fileType == FileType.Model )
                {
                    return ret;
                }

                ret.Variant = ushort.Parse( groups[ "variant" ].Value );
                return ret;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Parsing game path failed:\n{e}" );
                return new ObjectInfo() { FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleMonster( FileType fileType, ObjectType objectType, GroupCollection groups )
        {
            try
            {
                var ret = new MonsterInfo
                {
                    FileType   = fileType,
                    ObjectType = objectType,
                    MonsterId  = ushort.Parse( groups[ "monster" ].Value ),
                    BodyId     = ushort.Parse( groups[ "id" ].Value )
                };
                if( fileType == FileType.Imc || fileType == FileType.Model )
                {
                    return ret;
                }

                ret.Variant = byte.Parse( groups[ "variant" ].Value );
                return ret;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Parsing game path failed:\n{e}" );
                return new ObjectInfo() { FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleDemiHuman( FileType fileType, ObjectType objectType, GroupCollection groups )
        {
            try
            {
                var ret = new DemiHumanInfo
                {
                    FileType    = fileType,
                    ObjectType  = objectType,
                    DemihumanId = ushort.Parse( groups[ "id" ].Value ),
                    ItemId      = ushort.Parse( groups[ "equip" ].Value )
                };
                if( fileType == FileType.Imc )
                {
                    return ret;
                }

                ret.Slot = SlotToEquip[ groups[ "slot" ].Value ];
                if( fileType == FileType.Model )
                {
                    return ret;
                }

                ret.Variant = byte.Parse( groups[ "variant" ].Value );
                return ret;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Parsing game path failed:\n{e}" );
                return new ObjectInfo() { FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleCustomization( FileType fileType, ObjectType objectType, GroupCollection groups )
        {
            try
            {
                var ret = new CustomizationInfo
                {
                    FileType   = fileType,
                    ObjectType = objectType
                };
                if( groups[ "skin" ].Success )
                {
                    ret.Type = Customization.Skin;
                    return ret;
                }

                ret.Id = ushort.Parse( groups[ "id" ].Value );
                if( groups[ "location" ].Success )
                {
                    ret.Type = groups[ "location" ].Value == "face" ? Customization.DecalFace
                        : groups[ "location" ].Value == "equip"     ? Customization.DecalEquip : Customization.Unknown;
                    return ret;
                }

                var (gender, race) = IdToRace[ groups[ "race" ].Value ];
                ret.Gender         = gender;
                ret.Race           = race;
                ret.BodySlot       = SlotToBodyslot[ groups[ "type" ].Value ];
                ret.Type           = SlotToCustomization[ groups[ "slot" ].Value ];
                if( fileType == FileType.Material )
                {
                    ret.Variant = byte.Parse( groups[ "variant" ].Value );
                }

                return ret;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Parsing game path failed:\n{e}" );
                return new ObjectInfo() { FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleIcon( FileType fileType, ObjectType objectType, GroupCollection groups )
        {
            try
            {
                var ret = new IconInfo
                {
                    FileType   = fileType,
                    ObjectType = objectType,
                    Hq         = groups[ "hq" ].Success
                };
                if( groups[ "lang" ].Success )
                {
                    ret.Language = groups[ "lang" ].Value switch
                    {
                        "en" => Dalamud.ClientLanguage.English,
                        "ja" => Dalamud.ClientLanguage.Japanese,
                        "de" => Dalamud.ClientLanguage.German,
                        "fr" => Dalamud.ClientLanguage.French,
                        _    => ret.Language
                    };
                }

                ret.Id = uint.Parse( groups[ "id" ].Value );
                return ret;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Parsing game path failed:\n{e}" );
                return new ObjectInfo() { FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleMap( FileType fileType, ObjectType objectType, GroupCollection groups )
        {
            try
            {
                var ret = new MapInfo
                {
                    FileType   = fileType,
                    ObjectType = objectType
                };
                var map = groups[ "id" ].Value;
                ret.C1      = map[ 0 ];
                ret.C2      = map[ 1 ];
                ret.C3      = map[ 2 ];
                ret.C4      = map[ 3 ];
                ret.Variant = byte.Parse( groups[ "variant" ].Value );
                if( groups[ "suffix" ].Success )
                {
                    ret.Suffix = groups[ "suffix" ].Value[ 0 ];
                }

                return ret;
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Parsing game path failed:\n{e}" );
                return new ObjectInfo() { FileType = fileType, ObjectType = objectType };
            }
        }

        public static ObjectInfo GetFileInfo( GamePath path )
        {
            var (fileType, objectType, match) = ParseGamePath( path );
            if( match == null || !match.Success )
            {
                return new ObjectInfo() { FileType = fileType, ObjectType = objectType };
            }

            try
            {
                var groups = match.Groups;
                switch( objectType )
                {
                    case ObjectType.Accessory: return HandleEquipment( fileType, objectType, groups );
                    case ObjectType.Equipment: return HandleEquipment( fileType, objectType, groups );
                    case ObjectType.Weapon:    return HandleWeapon( fileType, objectType, groups );
                    case ObjectType.Map:       return HandleMap( fileType, objectType, groups );
                    case ObjectType.Monster:   return HandleMonster( fileType, objectType, groups );
                    case ObjectType.DemiHuman: return HandleDemiHuman( fileType, objectType, groups );
                    case ObjectType.Character: return HandleCustomization( fileType, objectType, groups );
                    case ObjectType.Icon:      return HandleIcon( fileType, objectType, groups );
                }
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not parse {path}:\n{e}" );
            }

            return new ObjectInfo() { FileType = fileType, ObjectType = objectType };
        }

        public static bool IsTailTexture( ObjectInfo info )
        {
            if( info is not CustomizationInfo )
            {
                return false;
            }

            var i = ( CustomizationInfo )info;
            return i.BodySlot == BodySlot.Tail && i.FileType == FileType.Texture;
        }

        public static bool IsSkinTexture( ObjectInfo info )
        {
            if( info is not CustomizationInfo )
            {
                return false;
            }

            var i = ( CustomizationInfo )info;
            return i.FileType == FileType.Texture && i.Type == Customization.Skin;
        }
    }
}