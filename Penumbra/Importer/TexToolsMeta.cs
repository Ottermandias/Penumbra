using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Penumbra.Game;
using Penumbra.Util;
using Swan;

namespace Penumbra.Importer
{
    public class MetaManipulation
    { }

    public class EqpManipulation
    { }

    public class TexToolsMeta
    {
        public class Info
        {
            private const string Pt   = @"(?'PrimaryType'[a-z]*)";                                              // language=regex
            private const string Pp   = @"(?'PrimaryPrefix'[a-z])";                                             // language=regex
            private const string Pi   = @"(?'PrimaryId'\d{4})";                                                 // language=regex
            private const string Pir  = @"\k'PrimaryId'";                                                       // language=regex
            private const string St   = @"(?'SecondaryType'[a-z]*)";                                            // language=regex
            private const string Sp   = @"(?'SecondaryPrefix'[a-z])";                                           // language=regex
            private const string Si   = @"(?'SecondaryId'\d{4})";                                               // language=regex
            private const string File = @"\k'PrimaryPrefix'\k'PrimaryId'(\k'SecondaryPrefix'\k'SecondaryId')?"; // language=regex
            private const string Slot = @"(_(?'Slot'[a-z]{3}))?";                                               // language=regex
            private const string Ext  = @"\.meta";

            private static readonly Regex HousingMeta = new( $"bgcommon/hou/{Pt}/general/{Pi}/{Pir}{Ext}" );
            private static readonly Regex CharaMeta   = new( $"chara/{Pt}/{Pp}{Pi}(/obj/{St}/{Sp}{Si})?/{File}{Slot}{Ext}" );

            public readonly  ObjectType PrimaryType;
            public readonly  BodySlot   SecondaryType;
            public readonly  ushort     PrimaryId;
            public readonly  ushort     SecondaryId;
            private readonly byte       _slot;

            public bool IsAccessory => PrimaryType == ObjectType.Accessory;
            public bool HasSecondary => SecondaryType != BodySlot.Unknown;

            public EquipSlot EquipSlot
            {
                get
                {
                    if( PrimaryType != ObjectType.Equipment && PrimaryType != ObjectType.Accessory )
                    {
                        throw new InvalidCastException();
                    }

                    return ( EquipSlot )_slot;
                }
            }

            public Customization Customization
            {
                get
                {
                    if( PrimaryType != ObjectType.Character )
                    {
                        throw new InvalidCastException();
                    }

                    return ( Customization )_slot;
                }
            }


            private static bool ValidType( ObjectType type )
            {
                return type switch
                {
                    ObjectType.Accessory     => true,
                    ObjectType.Character     => true,
                    ObjectType.Equipment     => true,
                    ObjectType.DemiHuman     => true,
                    ObjectType.Housing       => true,
                    ObjectType.Monster       => true,
                    ObjectType.Icon          => false,
                    ObjectType.Font          => false,
                    ObjectType.Interface     => false,
                    ObjectType.LoadingScreen => false,
                    ObjectType.Map           => false,
                    ObjectType.Vfx           => false,
                    ObjectType.Unknown       => false,
                    ObjectType.Weapon        => false,
                    ObjectType.World         => false,
                    _                        => false
                };
            }

            public Info( string fileName )
            {
                PrimaryType   = GamePathParser.PathToObjectType( new GamePath( fileName ) );
                PrimaryId     = 0;
                SecondaryType = BodySlot.Unknown;
                SecondaryId   = 0;
                _slot         = 0;
                if( !ValidType( PrimaryType ) )
                {
                    PrimaryType = ObjectType.Unknown;
                    return;
                }

                if( PrimaryType == ObjectType.Housing )
                {
                    var housingMatch = HousingMeta.Match( fileName );
                    if( housingMatch.Success )
                    {
                        PrimaryId = ushort.Parse( housingMatch.Groups[ "PrimaryId" ].Value );
                    }

                    return;
                }

                var match = CharaMeta.Match( fileName );
                if( !match.Success )
                {
                    return;
                }

                PrimaryId = ushort.Parse( match.Groups[ "PrimaryId" ].Value );
                if( !match.Groups[ "Slot" ].Success )
                {
                    return;
                }

                switch( PrimaryType )
                {
                    case ObjectType.Accessory:
                        if( GamePathParser.SlotToEquip.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpCust ) )
                        {
                            _slot = ( byte )tmpCust;
                        }

                        break;
                    case ObjectType.Equipment:
                    case ObjectType.Character:
                        if( GamePathParser.SlotToCustomization.TryGetValue( match.Groups[ "Slot" ].Value, out var tmpEquip ) )
                        {
                            _slot = ( byte )tmpEquip;
                        }

                        break;
                }

                if( match.Groups[ "SecondaryType" ].Success
                    && GamePathParser.SlotToBodyslot.TryGetValue( match.Groups[ "SecondaryType" ].Value, out SecondaryType ) )
                {
                    SecondaryId = ushort.Parse( match.Groups[ "SecondaryId" ].Value );
                }
            }
        }

        private enum MetaDataType : uint
        {
            Invalid = 0,
            Imc     = 1,
            Eqdp    = 2,
            Eqp     = 3,
            Est     = 4,
            Gmp     = 5
        };

        public readonly uint   Version;
        public readonly string FilePath;

        public readonly Info              MetaInfo;
        public readonly List< ImcEntry >  ImcEntries;
        public readonly EqpEntry?         EqpEntries;
        public readonly List< EqdpEntry > EqdpEntries;
        public readonly List< EstEntry >  EstEntries;
        public readonly GmpEntry?         GmpEntries;

        private static string ReadNullTerminated( BinaryReader reader )
        {
            var builder = new System.Text.StringBuilder();
            for( var c = reader.ReadChar(); c != 0; c = reader.ReadChar() )
            {
                builder.Append( c );
            }

            return builder.ToString();
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        public readonly struct EqpEntry
        {
            public static unsafe int Size => sizeof( EqpEntry );
            public readonly ulong Flags;

            public EqpEntry( byte[] data )
            {
                using var reader = new BinaryReader( new MemoryStream( data ) );
                Flags = reader.ReadUInt64();
            }
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        public readonly struct EstEntry
        {
            public static unsafe int Size => sizeof( EstEntry );
            public readonly ushort RaceCode;
            public readonly ushort SetId;
            public readonly ushort SkeletonId;

            public EstEntry( byte[] data )
            {
                using var reader = new BinaryReader( new MemoryStream( data ) );
                RaceCode   = reader.ReadUInt16();
                SetId      = reader.ReadUInt16();
                SkeletonId = reader.ReadUInt16();
            }
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        public readonly struct GmpEntry
        {
            public static unsafe int Size => sizeof( GmpEntry );
            private readonly uint _i;
            private readonly byte _b;

            public bool Enabled => ( _i & 1 ) == 1;
            public bool Animated => ( _i & 2 ) == 1;
            public ushort RotationA => ( ushort )( ( _i >> 2 ) & 0x3FF );
            public ushort RotationB => ( ushort )( ( _i >> 12 ) & 0x3FF );
            public ushort RotationC => ( ushort )( ( _i >> 22 ) & 0x3FF );
            public byte UnknownHigh => ( byte )( _b >> 4 );
            public byte UnknownLow => ( byte )( _b & 4 );

            public GmpEntry( byte[] data )
            {
                using var reader = new BinaryReader( new MemoryStream( data ) );
                _i = reader.ReadUInt32();
                _b = data[ 4 ];
            }
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        public readonly struct EqdpEntry
        {
            public static unsafe int Size => sizeof( EqdpEntry );
            public readonly uint RaceId;
            public readonly byte EntryData;

            [JsonIgnore]
            public bool Bit1 => ( EntryData & 0b01 ) == 0b01;

            [JsonIgnore]
            public bool Bit2 => ( EntryData & 0b10 ) == 0b10;

            public EqdpEntry( byte[] data )
            {
                using var reader = new BinaryReader( new MemoryStream( data ) );
                RaceId    = reader.ReadUInt32();
                EntryData = reader.ReadByte();
            }
        }

        [StructLayout( LayoutKind.Sequential, Pack = 1 )]
        public readonly struct ImcEntry
        {
            public static unsafe int Size => sizeof( ImcEntry );

            public readonly byte   Variant;
            public readonly byte   Decal;
            public readonly ushort Mask;
            public readonly byte   Vfx;
            public readonly byte   Anim;

            public ImcEntry( byte[] data )
            {
                using var reader = new BinaryReader( new MemoryStream( data ) );
                Variant = reader.ReadByte();
                Decal   = reader.ReadByte();
                Mask    = reader.ReadUInt16();
                Vfx     = reader.ReadByte();
                Anim    = reader.ReadByte();
            }
        }

        private static List< T > DeserializeEntry< T >( byte[] data, Func< byte[], T > factory, int sizeT )
        {
            if( data == null )
            {
                return new List< T >();
            }

            var       num     = data.Length / sizeT;
            List< T > entries = new();
            for( var i = 0; i < num; ++i )
            {
                entries.Add( factory( data.Slice( i, sizeT ) ) );
            }

            return entries;
        }

        public TexToolsMeta( byte[] data )
        {
            using var reader = new BinaryReader( new MemoryStream( data ) );
            Version  = reader.ReadUInt32();
            FilePath = ReadNullTerminated( reader );
            MetaInfo = new Info( FilePath );
            var numHeaders  = reader.ReadUInt32();
            var headerSize  = reader.ReadUInt32();
            var headerStart = reader.ReadUInt32();
            reader.BaseStream.Seek( headerStart, SeekOrigin.Begin );

            List< (MetaDataType type, uint offset, int size) > entries = new();
            for( var i = 0; i < numHeaders; ++i )
            {
                var currentOffset = reader.BaseStream.Position;
                var type          = ( MetaDataType )reader.ReadUInt32();
                var offset        = reader.ReadUInt32();
                var size          = reader.ReadInt32();
                entries.Add( ( type, offset, size ) );
                reader.BaseStream.Seek( currentOffset + headerSize, SeekOrigin.Begin );
            }

            byte[] ReadEntry( MetaDataType type )
            {
                var idx = entries.FindIndex( t => t.type == type );
                if( idx < 0 )
                {
                    return null;
                }

                reader.BaseStream.Seek( entries[ idx ].offset, SeekOrigin.Begin );
                return reader.ReadBytes( entries[ idx ].size );
            }

            ImcEntries  = DeserializeEntry( ReadEntry( MetaDataType.Imc ), b => new ImcEntry( b ), ImcEntry.Size );
            EqdpEntries = DeserializeEntry( ReadEntry( MetaDataType.Eqdp ), b => new EqdpEntry( b ), EqdpEntry.Size );
            EstEntries  = DeserializeEntry( ReadEntry( MetaDataType.Est ), b => new EstEntry( b ), EstEntry.Size );

            var gmpEntry = ReadEntry( MetaDataType.Gmp );
            GmpEntries = gmpEntry == null ? null : new GmpEntry( gmpEntry );

            var eqpEntry = ReadEntry( MetaDataType.Eqp );
            EqpEntries = gmpEntry == null ? null : new EqpEntry( eqpEntry );
        }
    }
}