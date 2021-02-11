namespace Penumbra.GameFiles
{
    public enum ObjectType : byte
    {
        Unknown,
        Vfx,
        Demihuman,
        Accessory,
        World,
        Housing,
        Monster,
        Icon,
        LoadingScreen,
        Map,
        Interface,
        Equipment,
        Character,
        Weapon,
        Font,
    }

    public enum Gender : byte
    {
        Unknown,
        Male,
        Female,
        MaleNPC,
        FemaleNPC,
    }

    public enum Race : byte
    {
        Unknown,
        Midlander,
        Highlander,
        Elezen,
        Lalafell,
        Miqote,
        Roegadyn,
        AuRa,
        Hrothgar,
        Viera,
    }

    public enum EquipSlot : byte
    {
        Unknown           =  0,
        MainHand          =  1,
        Offhand           =  2,
        Head              =  3,
        Body              =  4,
        Hands             =  5,
        Belt              =  6,
        Legs              =  7,
        Feet              =  8,
        Ears              =  9,
        Neck              = 10,
        RingR             = 12,
        RingL             = 12,
        Wrists            = 11,
        BothHand          = 13,
        HeadBody          = 15,
        BodyHandsLegsFeet = 16,
        SoulCrystal       = 17,
        LegsFeet          = 18,
        FullBody          = 19,
        BodyHands         = 20,
        BodyLegsFeet      = 21,
        All               = 22
    }

    public enum BodySlot : byte
    {
        Unknown,
        Hair,
        Face,
        Tail,
        Body,
        Zear,
    }

    public enum Customization : byte
    {
        Unknown,
        Body, 
        Tail, 
        Face,
        Iris,
        Accessory,
        Hair,
        DecalFace,
        DecalEquip,
        Skin,
        Etc,
    }

    public enum FileType : byte
    {
        Unknown,
        Sound,
        Imc,
        Vfx,
        Animation,
        Pap,
        MetaInfo,
        Material,
        Texture,
        Model,
        Shader,
        Font,
        Environment,
    }
}
