using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using Dalamud.Plugin;

namespace Penumbra.GameFiles
{
    public static class GamePathParser
    {
        private const string CharacterFolder = "chara/";
        private const string EquipmentFolder = "equipment/";
        private const string PlayerFolder    = "human/";
        private const string WeaponFolder    = "weapon/";
        private const string AccessoryFolder = "accessory/";
        private const string DemihumanFolder = "demihuman/";
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

        private static readonly Dictionary<FileType, Dictionary<ObjectType, Regex[]>> Regexes = new()
        { { FileType.Font, new(){ { ObjectType.Font, new Regex[]{ new(@"common/font/(?'fontname'.*)_(?'id'\d\d)(_lobby)?\.fdt") } } } }
        , { FileType.Texture, new()
            { { ObjectType.Icon,      new Regex[]{ new(@"ui/icon/(?'group'\d*)(/(?'lang'[a-z]{2}))?(/(?'hq'hq))?/(?'id'\d*)\.tex") } }
            , { ObjectType.Map,       new Regex[]{ new(@"ui/map/(?'id'[a-z0-9]{4})/(?'variant'\d{2})/\k'id'\k'variant'(?'suffix'[a-z])?(_[a-z])?\.tex")  } }
            , { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'weapon'\d{4})/obj/body/b(?'id'\d{4})/texture/v(?'variant'\d{2})_w\k'weapon'b\k'id'(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/texture/v(?'variant'\d{2})_m\k'monster'b\k'id'(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/texture/v(?'variant'\d{2})_c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Demihuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/texture/v(?'variant'\d{2})_d\k'id'e\k'equip'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/texture/v(?'variant'\d{2})_c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})_[a-z]\.tex") } }
            , { ObjectType.Character, new Regex[]{ new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/texture/(?'minus'(--)?)c\k'race'\k'typeabr'\k'id'_(?'slot'[a-z]{3})(_[a-z])?_[a-z]\.tex")
                                                 , new(@"chara/common/texture/skin(?'skin'.*)\.tex")
                                                 , new(@"chara/common/texture/decal_(?'location'[a-z]+)/[-_]?decal_(?'id'\d+).tex") } } } }
        , { FileType.Model, new()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'weapon'\d{4})/obj/body/b(?'id'\d{4})/model/w\k'weapon'b\k'id'\.mdl") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/model/m\k'monster'b\k'id'\.mdl") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/model/c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.Demihuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/model/d\k'id'e\k'equip'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/model/c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})\.mdl") } }
            , { ObjectType.Character, new Regex[]{ new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/model/c\k'race'\k'typeabr'\k'id'_(?'slot'[a-z]{3})\.mdl") } } } }
        , { FileType.Material, new()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'weapon'\d{4})/obj/body/b(?'id'\d{4})/material/v(?'variant'\d{4})/mt_w\k'weapon'b\k'id'_[a-z]\.mtrl") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/material/v(?'variant'\d{4})/mt_m\k'monster'b\k'id'_[a-z]\.mtrl") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c(?'race'\d{4})e\k'id'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.Demihuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/material/v(?'variant'\d{4})/mt_d\k'id'e\k'equip'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c(?'race'\d{4})a\k'id'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } }
            , { ObjectType.Character, new Regex[]{ new(@"chara/human/c(?'race'\d{4})/obj/(?'type'[a-z]+)/(?'typeabr'[a-z])(?'id'\d{4})/material/v(?'variant'\d{4})/mt_c\k'race'\k'typeabr'\k'id'_(?'slot'[a-z]{3})_[a-z]\.mtrl") } } } }
        , { FileType.Imc, new()
            { { ObjectType.Weapon,    new Regex[]{ new(@"chara/weapon/w(?'weapon'\d{4})/obj/body/b(?'id'\d{4})/b\k'id'\.imc") } }
            , { ObjectType.Monster,   new Regex[]{ new(@"chara/monster/m(?'monster'\d{4})/obj/body/b(?'id'\d{4})/b\k'id'\.imc") } }
            , { ObjectType.Equipment, new Regex[]{ new(@"chara/equipment/e(?'id'\d{4})/e\k'id'\.imc") } }
            , { ObjectType.Demihuman, new Regex[]{ new(@"chara/demihuman/d(?'id'\d{4})/obj/equipment/e(?'equip'\d{4})/e\k'equip'\.imc") } }
            , { ObjectType.Accessory, new Regex[]{ new(@"chara/accessory/a(?'id'\d{4})/a\k'id'\.imc") } } } }
        };

        

        public static ObjectType PathToObjectType(GamePath path)
        {
            string p = path;
            if (p.StartsWith(CharacterFolder))
            {
                p = p.Substring(CharacterFolder.Length);

                if (p.StartsWith(EquipmentFolder))
                    return ObjectType.Equipment;
                if (p.StartsWith(AccessoryFolder))
                    return ObjectType.Accessory;
                if (p.StartsWith(WeaponFolder))
                    return ObjectType.Weapon;
                if (p.StartsWith(PlayerFolder))
                    return ObjectType.Character;
                if (p.StartsWith(DemihumanFolder))
                    return ObjectType.Demihuman;
                if (p.StartsWith(MonsterFolder))
                    return ObjectType.Monster;
                if (p.StartsWith(CommonFolder))
                    return ObjectType.Character;
                return ObjectType.Unknown;
            }
            if (p.StartsWith(UiFolder))
            {
                p = p.Substring(UiFolder.Length);
                if (p.StartsWith(IconFolder))
                    return ObjectType.Icon;
                if (p.StartsWith(LoadingFolder))
                    return ObjectType.LoadingScreen;
                if (p.StartsWith(MapFolder))
                    return ObjectType.Map;
                if (p.StartsWith(InterfaceFolder))
                    return ObjectType.Interface;
                return ObjectType.Unknown;
            }
            if (p.StartsWith(FontFolder))
                return ObjectType.Font;

            if( p.StartsWith(HousingFolder) )
                return ObjectType.Housing;
            if (p.StartsWith(WorldFolder1) || p.StartsWith(WorldFolder2))
                return ObjectType.World;
            if (p.StartsWith(VfxFolder))
                return ObjectType.Vfx;

            return ObjectType.Unknown;
        }

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
        , { "0104", (Gender.MaleNPC,   Race.Midlander)  }
        , { "0201", (Gender.Female,    Race.Midlander)  }
        , { "0204", (Gender.FemaleNPC, Race.Midlander)  }
        , { "0301", (Gender.Male,      Race.Highlander) }
        , { "0304", (Gender.MaleNPC,   Race.Highlander) }
        , { "0401", (Gender.Female,    Race.Highlander) }
        , { "0404", (Gender.FemaleNPC, Race.Highlander) }
        , { "0501", (Gender.Male,      Race.Elezen)     }
        , { "0504", (Gender.MaleNPC,   Race.Elezen)     }
        , { "0601", (Gender.Female,    Race.Elezen)     }
        , { "0604", (Gender.FemaleNPC, Race.Elezen)     }
        , { "0701", (Gender.Male,      Race.Miqote)     }
        , { "0704", (Gender.MaleNPC,   Race.Miqote)     }
        , { "0801", (Gender.Female,    Race.Miqote)     }
        , { "0804", (Gender.FemaleNPC, Race.Miqote)     }
        , { "0901", (Gender.Male,      Race.Roegadyn)   }
        , { "0904", (Gender.MaleNPC,   Race.Roegadyn)   }
        , { "1001", (Gender.Female,    Race.Roegadyn)   }
        , { "1004", (Gender.FemaleNPC, Race.Roegadyn)   }
        , { "1101", (Gender.Male,      Race.Lalafell)   }
        , { "1104", (Gender.MaleNPC,   Race.Lalafell)   }
        , { "1201", (Gender.Female,    Race.Lalafell)   }
        , { "1204", (Gender.FemaleNPC, Race.Lalafell)   }
        , { "1301", (Gender.Male,      Race.AuRa)       }
        , { "1304", (Gender.MaleNPC,   Race.AuRa)       }
        , { "1401", (Gender.Female,    Race.AuRa)       }
        , { "1404", (Gender.FemaleNPC, Race.AuRa)       }
        , { "1501", (Gender.Male,      Race.Hrothgar)   }
        , { "1504", (Gender.MaleNPC,   Race.Hrothgar)   }
        , { "1801", (Gender.Female,    Race.Viera)      }
        , { "1804", (Gender.FemaleNPC, Race.Viera)      }
        , { "9104", (Gender.MaleNPC,   Race.Unknown)    }
        , { "9204", (Gender.FemaleNPC, Race.Unknown)    }
        };

        private static (FileType, ObjectType, Match) ParseGamePath(GamePath path)
        {
            if (!ExtensionToType.TryGetValue(Extension(path), out var fileType))
                fileType = FileType.Unknown;

            var objectType = PathToObjectType(path);

            if (!Regexes.TryGetValue(fileType, out var objectDict))
                return (fileType, objectType, null);

            if (!objectDict.TryGetValue(objectType, out var regexes))
                return (fileType, objectType, null);

            foreach (var regex in regexes)
            {
                var match = regex.Match(path);
                if (match.Success)
                    return (fileType, objectType, match);
            }
            return (fileType, objectType, null);
        }

        private static string Extension(string filename)
        {
            var extIdx = filename.LastIndexOf('.');
            if (extIdx < 0)
                return "";
            return filename.Substring(extIdx);
        }

        private static ObjectInfo HandleEquipment(FileType fileType, ObjectType objectType, GroupCollection groups)
        {
            try
            {
                EquipInfo ret = new(){ FileType = fileType, ObjectType = objectType };
                ret.ItemId = ushort.Parse(groups["id"].Value);
                if(fileType == FileType.Imc)
                    return ret;
                var (gender, race) = IdToRace[groups["race"].Value];
                ret.Gender = gender;
                ret.Race   = race;
                ret.Slot   = SlotToEquip[groups["slot"].Value];
                if (fileType == FileType.Model)
                    return ret;
                ret.Variant = ushort.Parse(groups["variant"].Value);
                return ret;
            }
            catch(Exception e)
            {
                PluginLog.Error($"Parsing game path failed:\n{e}");
                return new ObjectInfo(){ FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleWeapon(FileType fileType, ObjectType objectType, GroupCollection groups)
        {
            try
            {
                WeaponInfo ret = new(){ FileType = fileType, ObjectType = objectType };
                ret.ItemId = ushort.Parse(groups["weapon"].Value);
                ret.Set = ushort.Parse(groups["id"].Value);
                if(fileType == FileType.Imc || fileType == FileType.Model)
                    return ret;
                ret.Variant = ushort.Parse(groups["variant"].Value);
                return ret;
            }
            catch(Exception e)
            {
                PluginLog.Error($"Parsing game path failed:\n{e}");
                return new ObjectInfo(){ FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleMonster(FileType fileType, ObjectType objectType, GroupCollection groups)
        {
            try
            {
                MonsterInfo ret = new(){ FileType = fileType, ObjectType = objectType };
                ret.MonsterId = ushort.Parse(groups["monster"].Value);
                ret.BodyId = ushort.Parse(groups["id"].Value);
                if(fileType == FileType.Imc || fileType == FileType.Model)
                    return ret;
                ret.Variant = byte.Parse(groups["variant"].Value);
                return ret;
            }
            catch(Exception e)
            {
                PluginLog.Error($"Parsing game path failed:\n{e}");
                return new ObjectInfo(){ FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleDemihuman(FileType fileType, ObjectType objectType, GroupCollection groups)
        {
            try
            {
                DemihumanInfo ret = new(){ FileType = fileType, ObjectType = objectType };
                ret.DemihumanId = ushort.Parse(groups["id"].Value);
                ret.ItemId = ushort.Parse(groups["equip"].Value);
                if (fileType == FileType.Imc)
                    return ret;
                ret.Slot = SlotToEquip[groups["slot"].Value];
                if (fileType == FileType.Model)
                    return ret;
                ret.Variant = byte.Parse(groups["variant"].Value);
                return ret;
            }
            catch(Exception e)
            {
                PluginLog.Error($"Parsing game path failed:\n{e}");
                return new ObjectInfo(){ FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleCustomization(FileType fileType, ObjectType objectType, GroupCollection groups)
        {
            try
            {
                CustomizationInfo ret = new(){ FileType = fileType, ObjectType = objectType };
                if (groups["skin"].Success)
                {
                    ret.Type = Customization.Skin;
                    return ret;
                }

                ret.Id = ushort.Parse(groups["id"].Value);
                if (groups["location"].Success)
                {
                    ret.Type = groups["location"].Value == "face"  ? Customization.DecalFace 
                             : groups["location"].Value == "equip" ? Customization.DecalEquip : Customization.Unknown;
                    return ret;
                }

                var (gender, race) = IdToRace[groups["race"].Value];
                ret.Gender   = gender;
                ret.Race     = race;
                ret.BodySlot = SlotToBodyslot[groups["type"].Value];
                ret.Type     = SlotToCustomization[groups["slot"].Value];
                if (fileType == FileType.Material)
                    ret.Variant = byte.Parse(groups["variant"].Value);
                return ret;
            }
            catch(Exception e)
            {
                PluginLog.Error($"Parsing game path failed:\n{e}");
                return new ObjectInfo(){ FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleIcon(FileType fileType, ObjectType objectType, GroupCollection groups)
        {
            try
            {
                IconInfo ret = new(){ FileType = fileType, ObjectType = objectType };
                ret.Hq = groups["hq"].Success;
                if (groups["lang"].Success)
                {
                    switch(groups["lang"].Value)
                    {
                        case "en": ret.Language = Dalamud.ClientLanguage.English;  break;
                        case "ja": ret.Language = Dalamud.ClientLanguage.Japanese; break;
                        case "de": ret.Language = Dalamud.ClientLanguage.German;   break;
                        case "fr": ret.Language = Dalamud.ClientLanguage.French;   break;
                    }
                }
                ret.Id = uint.Parse(groups["id"].Value);
                return ret;
            }
            catch(Exception e)
            {
                PluginLog.Error($"Parsing game path failed:\n{e}");
                return new ObjectInfo(){ FileType = fileType, ObjectType = objectType };
            }
        }

        private static ObjectInfo HandleMap(FileType fileType, ObjectType objectType, GroupCollection groups)
        {
            try
            {
                MapInfo ret = new(){ FileType = fileType, ObjectType = objectType };
                var map = groups["id"].Value;
                ret.C1 = map[0];
                ret.C2 = map[1];
                ret.C3 = map[2];
                ret.C4 = map[3];
                ret.Variant = byte.Parse(groups["variant"].Value);
                if (groups["suffix"].Success)
                    ret.Suffix = groups["suffix"].Value[0];
                return ret;
            }
            catch(Exception e)
            {
                PluginLog.Error($"Parsing game path failed:\n{e}");
                return new ObjectInfo(){ FileType = fileType, ObjectType = objectType };
            }
        }

        public static ObjectInfo GetFileInfo(GamePath path)
        {
            var (fileType, objectType, match) = ParseGamePath(path);
            if (match == null || !match.Success)
                return new (){ FileType = fileType, ObjectType = objectType };
            try
            {
                var groups = match.Groups;
                switch (objectType)
                {
                    case ObjectType.Accessory: return HandleEquipment    (fileType, objectType, groups);
                    case ObjectType.Equipment: return HandleEquipment    (fileType, objectType, groups);
                    case ObjectType.Weapon:    return HandleWeapon       (fileType, objectType, groups);
                    case ObjectType.Map:       return HandleMap          (fileType, objectType, groups);
                    case ObjectType.Monster:   return HandleMonster      (fileType, objectType, groups);
                    case ObjectType.Demihuman: return HandleDemihuman    (fileType, objectType, groups);
                    case ObjectType.Character: return HandleCustomization(fileType, objectType, groups);
                    case ObjectType.Icon:      return HandleIcon         (fileType, objectType, groups);
                }
            }
            catch(Exception e)
            {
                PluginLog.Error($"Could not parse {path}:\n{e}");
            }
            return new(){ FileType = fileType, ObjectType = objectType };            
        }

        public static bool IsTailTexture(ObjectInfo info)
        {
            if (info is not CustomizationInfo)
                return false;
            var i = info as CustomizationInfo;
            return i.BodySlot == BodySlot.Tail && i.FileType == FileType.Texture;
        }

        public static bool IsSkinTexture(ObjectInfo info)
        {
            if (info is not CustomizationInfo)
                return false;
            var i = info as CustomizationInfo;
            return i.FileType == FileType.Texture && i.Type == Customization.Skin;
        }
    }
}
