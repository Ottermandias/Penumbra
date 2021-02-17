using System;
using Lumina.Excel.GeneratedSheets;

namespace Penumbra.Game
{
    public class ObjectInfo : IComparable
    {
        protected const int BitUsed = 16;
        public FileType   FileType{ get; set; }
        public ObjectType ObjectType{ get; set; }

        protected virtual ulong ToLong() { return ((ulong) FileType) | (((ulong) ObjectType) << 8); }

        public override int GetHashCode()
        {
            return ToLong().GetHashCode();
        }

        public int CompareTo(object r)
        {
            if (r == null)
                return 1;

            if (r is ItemInfo)
            {
                return ToLong().CompareTo((r as ObjectInfo).ToLong());
            }
            return 1;
        }

        public virtual bool CompatibleWith(Item i){ throw new NotImplementedException();}
    }

    public class ItemInfo : ObjectInfo
    {
        protected new const int BitUsed = ObjectInfo.BitUsed + 32;
        public ushort ItemId{ get; set; }
        public ushort Variant{ get; set; } = 0;

        protected override ulong ToLong()
        {
            var x = base.ToLong();
            x |= (ulong) ItemId  << (ObjectInfo.BitUsed +  0);
            x |= (ulong) Variant << (ObjectInfo.BitUsed + 16);
            return x;
        }
    }

    public class EquipInfo : ItemInfo
    {
        protected new const int BitUsed = ItemInfo.BitUsed + 5 + 3 + 4;
        public EquipSlot Slot{ get; set; } = EquipSlot.All;
        public Gender Gender{ get; set; } = Gender.Unknown;
        public Race Race{ get; set; } = Race.Unknown;

        protected override ulong ToLong()
        {
            var x = base.ToLong();
            x |= (ulong) Slot   << (ItemInfo.BitUsed + 0);
            x |= (ulong) Gender << (ItemInfo.BitUsed + 5);
            x |= (ulong) Race   << (ItemInfo.BitUsed + 8);
            return x;
        }

        public override bool CompatibleWith(Item i)
        {
            if (i.EquipSlotCategory.Row != (int) Slot)
                return false;
            if (((i.ModelMain & 0xFFFF) == ItemId && (Variant == 0 || ((i.ModelMain >> 16) & 0xFFFF) == Variant)))
                return true;
            if (((i.ModelSub & 0xFFFF) == ItemId && (Variant == 0 || ((i.ModelSub >> 16) & 0xFFFF) == Variant)))
                return true;
            return false;
        }
    }

    public class WeaponInfo : ItemInfo
    {
        protected new const int BitUsed = ItemInfo.BitUsed + 16;
        public ushort Set{ get; set; } = 0;

        protected override ulong ToLong()
        {
            var x = base.ToLong();
            x |= (ulong) Set << ItemInfo.BitUsed;
            return x;
        }

        public override bool CompatibleWith(Item i)
        {
            switch (i.EquipSlotCategory.Row)
            {
            case (int) EquipSlot.BothHand:
            case (int) EquipSlot.MainHand:
            case (int) EquipSlot.Offhand:
                if (((i.ModelMain & 0xFFFF) == ItemId
                    && ((i.ModelMain >> 16) & 0xFFFF) == Set)
                    && (Variant == 0 || ((i.ModelMain >> 32) & 0xFFFF) == Variant))
                    return true;
                if (((i.ModelSub & 0xFFFF) == ItemId
                    && ((i.ModelSub >> 16) & 0xFFFF) == Set)
                    && (Variant == 0 || ((i.ModelSub >> 32) & 0xFFFF) == Variant))
                    return true;
                return false;
            }

            return false;
        }
    }

    public class IconInfo : ObjectInfo
    {
        protected new const int BitUsed = ObjectInfo.BitUsed + 32 + 1 + 1 + 2;
        public uint Id{ get; set; }
        public bool Hq{ get; set; } = false;
        public Dalamud.ClientLanguage? Language{ get; set; } = null;

        protected override ulong ToLong()
        {
            var x = base.ToLong();
            x |= (ulong) Id << ObjectInfo.BitUsed;
            if (Hq)
                x |= 1ul << (ObjectInfo.BitUsed + 32);
            if (Language != null)
            {
                x |= 1ul << (ObjectInfo.BitUsed + 33);
                x |= (ulong) Language.Value << (ObjectInfo.BitUsed + 34);
            }

            return x;
        }
    }

    public class CustomizationInfo : ObjectInfo
    {
        protected new const int BitUsed = ObjectInfo.BitUsed + 3 + 4 + 4 + 4 + 8 + 1 + 16 ;
        public Gender Gender { get; set; } = Gender.Unknown;
        public Race   Race   { get; set; } = Race.Unknown;
        public BodySlot BodySlot  { get; set; } = BodySlot.Unknown;
        public Customization Type { get; set; } = Customization.Unknown;
        public byte Variant { get; set; } = 0;
        public ushort? Id{ get; set; }

        protected override ulong ToLong()
        {
            var x = base.ToLong();
            x |= (ulong) Gender  << (ObjectInfo.BitUsed + 0);
            x |= (ulong) Race    << (ObjectInfo.BitUsed + 3);
            x |= (ulong) Type    << (ObjectInfo.BitUsed + 7);
            x |= (ulong) Variant << (ObjectInfo.BitUsed + 11);
            if (Id != null)
            {
                x |= 1ul << ObjectInfo.BitUsed + 19;
                x |= (ulong) Id.Value << (ObjectInfo.BitUsed + 21);
            }
            return x;
        }
    }

    public class MapInfo : ObjectInfo
    {
        protected new const int BitUsed = ObjectInfo.BitUsed + 32 + 8 + 8;
        public char C1 {get; set; }
        public char C2 {get; set; }
        public char C3 {get; set; }
        public char C4 {get; set; }
        public byte Variant { get; set; }
        public char? Suffix { get; set; } = null;

        protected override ulong ToLong()
        {
            var x = base.ToLong();
            x |= (ulong)      C1 << (ObjectInfo.BitUsed +  0);
            x |= (ulong)      C2 << (ObjectInfo.BitUsed +  8);
            x |= (ulong)      C3 << (ObjectInfo.BitUsed + 16);
            x |= (ulong)      C4 << (ObjectInfo.BitUsed + 24);
            x |= (ulong) Variant << (ObjectInfo.BitUsed + 32);
            if (Suffix != null)
                x |= (ulong) Suffix.Value << (ObjectInfo.BitUsed + 40);

            return x;
        }
    }

    public class MonsterInfo : ObjectInfo
    {
        protected new const int BitUsed = ObjectInfo.BitUsed + 16 + 16 + 8;
        public ushort MonsterId { get; set; }
        public ushort BodyId { get; set; }
        public byte Variant { get; set; } = 0;

        protected override ulong ToLong()
        {
            var x = base.ToLong();
            x |= (ulong) MonsterId << (ObjectInfo.BitUsed +  0);
            x |= (ulong)    BodyId << (ObjectInfo.BitUsed + 16);
            x |= (ulong)   Variant << (ObjectInfo.BitUsed + 32);
            return x;
        }
    }

    public class DemiHumanInfo : ObjectInfo
    {
        protected new const int BitUsed = ObjectInfo.BitUsed + 16 + 16 + 8 + 8;
        public ushort DemihumanId { get; set; }
        public ushort ItemId { get; set; }
        public byte Variant { get; set; } = 0;
        public EquipSlot Slot { get; set; } = EquipSlot.Unknown;

        protected override ulong ToLong()
        {
            var x = base.ToLong();
            x |= (ulong) DemihumanId << (ObjectInfo.BitUsed +  0);
            x |= (ulong)      ItemId << (ObjectInfo.BitUsed + 16);
            x |= (ulong)     Variant << (ObjectInfo.BitUsed + 32);
            x |= (ulong)        Slot << (ObjectInfo.BitUsed + 40);
            return x;
        }
    }
}
