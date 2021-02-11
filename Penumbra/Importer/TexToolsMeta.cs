using System.Collections.Generic;
using System.IO;
using System;
using System.Collections;
using System.Net.Security;

namespace Penumbra.Importer
{
    public static class Extensions
    {
        public static T[] Slice<T>(this T[] source, int index, int length)
        {       
            var slice = new T[length];
            Array.Copy(source, index, slice, 0, length);
            return slice;
        }
    }

    public class TexToolsMeta
    {
        private enum MetaDataType : uint
        {
            Invalid = 0,
            Imc     = 1,
            Eqdp    = 2,
            Eqp     = 3,
            Est     = 4,
            Gmp     = 5,
        };

        public readonly uint   Version;
        public readonly string FilePath;
        public readonly List< ImcEntry>  ImcEntries;
        public readonly List< EqpEntry>  EqpEntries;
        public readonly List<EqdpEntry> EqdpEntries;
        public readonly List< EstEntry>  EstEntries;
        public readonly GmpEntry?        GmpEntries;

        private static string ReadNullTerminated(BinaryReader reader)
        {
            var builder = new System.Text.StringBuilder();
            for (var c = reader.ReadChar(); c != 0; c = reader.ReadChar())
                builder.Append(c);
            return builder.ToString();
        }
        
        public struct EqpEntry
        {
            public static unsafe int Size { get{ return sizeof(EqpEntry); } }
            public readonly ulong Flags;

            public EqpEntry(byte[] data)
            {
                using var reader = new BinaryReader(new MemoryStream(data));
                Flags = reader.ReadUInt64();
            }
        }

        public struct EstEntry
        {
            public static unsafe int Size { get{ return sizeof(EstEntry); } }
            public readonly ushort RaceCode;
            public readonly ushort SetId;
            public readonly ushort SkeletonId;

            public EstEntry(byte[] data)
            {
                using var reader = new BinaryReader(new MemoryStream(data));
                RaceCode   = reader.ReadUInt16();
                SetId      = reader.ReadUInt16();
                SkeletonId = reader.ReadUInt16();
            }
        }

        public struct GmpEntry
        {
            public static unsafe int Size { get{ return sizeof(GmpEntry); } }
            private readonly uint _i;
            private readonly byte _b;

            public bool   Enabled     { get{ return (_i & 1) == 1; }}
            public bool   Animated    { get{ return (_i & 2) == 1; }}
            public ushort RotationA   { get{ return (ushort) ((_i >>  2) & 0x3FF); }}
            public ushort RotationB   { get{ return (ushort) ((_i >> 12) & 0x3FF); }}
            public ushort RotationC   { get{ return (ushort) ((_i >> 22) & 0x3FF); }}
            public byte   UnknownHigh { get{ return (byte) (_b >> 4); }}
            public byte   UnknownLow  { get{ return (byte) (_b & 4); }}

            public GmpEntry(byte[] data)
            {
                using var reader = new BinaryReader(new MemoryStream(data));
                _i = reader.ReadUInt32();
                _b = data[4];
            }
        }

        public struct EqdpEntry
        {
            public static unsafe int Size { get{ return sizeof(EqdpEntry); } }
            public readonly uint RaceId;
            public readonly byte EntryData;

            public EqdpEntry(byte[] data)
            {
                using var reader = new BinaryReader(new MemoryStream(data));
                RaceId    = reader.ReadUInt32();
                EntryData = reader.ReadByte();
            }
        }

        public struct ImcEntry
        {
            public static unsafe int Size { get{ return sizeof(ImcEntry); } }

            public readonly byte   Variant;
            public readonly byte   Decal;
            public readonly ushort Mask;
            public readonly byte   Vfx;
            public readonly byte   Anim;

            public ImcEntry(byte[] data)
            {
                using var reader = new BinaryReader(new MemoryStream(data));
                Variant = reader.ReadByte();
                Decal   = reader.ReadByte();
                Mask    = reader.ReadUInt16();
                Vfx     = reader.ReadByte();
                Anim    = reader.ReadByte();
            }
        }

        private static List<T> DeserializeEntry<T>(byte[] data, Func<byte[], T> factory, int sizeT)
        {
            if (data == null)
                return new();

            var num = data.Length / sizeT;
            List<T> entries = new();
            for (var i = 0; i < num; ++i)
                entries.Add(factory(data.Slice(i, sizeT)));
            return entries;
        }

        public TexToolsMeta(byte[] data)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            Version = reader.ReadUInt32();
            FilePath = ReadNullTerminated(reader);
            var numHeaders = reader.ReadUInt32();
            var headerSize = reader.ReadUInt32();
            var headerStart = reader.ReadUInt32();
            reader.BaseStream.Seek(headerStart, SeekOrigin.Begin);

            List<(MetaDataType type, uint offset, int size)> entries = new();
            for (var i = 0; i < numHeaders; ++i)
            {
                var currentOffset = reader.BaseStream.Position;
                var type = (MetaDataType) reader.ReadUInt32();
                var offset = reader.ReadUInt32();
                var size = reader.ReadInt32();
                entries.Add((type, offset, size));
                reader.BaseStream.Seek(currentOffset + headerSize, SeekOrigin.Begin);
            }

            byte[] ReadEntry(MetaDataType type)
            {
                var idx = entries.FindIndex( t => t.type == type);
                if (idx < 0)
                    return null;
                reader.BaseStream.Seek(entries[idx].offset, SeekOrigin.Begin);
                return reader.ReadBytes(entries[idx].size);
            }
            
            ImcEntries  = DeserializeEntry< ImcEntry>(ReadEntry(MetaDataType.Imc) , b => new(b),  ImcEntry.Size);
            EqpEntries  = DeserializeEntry< EqpEntry>(ReadEntry(MetaDataType.Eqp) , b => new(b),  EqpEntry.Size);
            EqdpEntries = DeserializeEntry<EqdpEntry>(ReadEntry(MetaDataType.Eqdp), b => new(b), EqdpEntry.Size);
            EstEntries  = DeserializeEntry< EstEntry>(ReadEntry(MetaDataType.Est) , b => new(b),  EstEntry.Size);

            var gmpEntry = ReadEntry(MetaDataType.Gmp);
            GmpEntries  = gmpEntry == null ? null : new(gmpEntry);
        }
    }
}