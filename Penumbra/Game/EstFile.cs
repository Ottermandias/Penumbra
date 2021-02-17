using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumina.Data;

namespace Penumbra.Game
{
    public class EstFile
    {
        private const ushort EntryDescSize = 4;
        private const ushort EntrySize     = 2;

        public FileResource File { get; }

        private readonly Dictionary< (Gender, Race), Dictionary< ushort, ushort > > _entries = new();
        private uint NumEntries { get; set; }

        private void DeleteEntry( Gender gender, Race race, ushort setId )
        {
            if( !_entries.TryGetValue( ( gender, race ), out var setDict ) )
            {
                return;
            }

            if( !setDict.ContainsKey( setId ) )
            {
                return;
            }

            setDict.Remove( setId );
            if( setDict.Count == 0 )
            {
                _entries.Remove( ( gender, race ) );
            }

            --NumEntries;
        }

        private bool AddEntry( Gender gender, Race race, ushort setId, ushort entry )
        {
            if( !_entries.TryGetValue( ( gender, race ), out var setDict ) )
            {
                _entries[ ( gender, race ) ] = new Dictionary< ushort, ushort >();
                setDict                      = _entries[ ( gender, race ) ];
            }

            var ret = !setDict.ContainsKey( setId );
            setDict[ setId ] = entry;
            return ret;
        }

        public void SetEntry( Gender gender, Race race, ushort setId, ushort entry )
        {
            if( entry != 0 )
            {
                if( AddEntry( gender, race, setId, entry ) )
                {
                    ++NumEntries;
                }
            }
            else
            {
                DeleteEntry( gender, race, setId );
            }
        }

        public ushort GetEntry( Gender gender, Race race, ushort setId )
        {
            if( !_entries.TryGetValue( ( gender, race ), out var setDict ) )
            {
                return 0;
            }

            return !setDict.TryGetValue( setId, out var entry ) ? 0 : entry;
        }

        public byte[] WriteBytes()
        {
            using MemoryStream mem = new( ( int )( 4 + ( EntryDescSize + EntrySize ) * NumEntries ) );
            using BinaryWriter bw  = new( mem );

            bw.Write( NumEntries );
            foreach( var kvp1 in _entries )
            {
                foreach( var kvp2 in kvp1.Value )
                {
                    bw.Write( kvp2.Key );
                    bw.Write( GamePathParser.RaceToId[ kvp1.Key ] );
                }
            }

            foreach( var kvp2 in _entries.SelectMany( kvp1 => kvp1.Value ) )
            {
                bw.Write( kvp2.Value );
            }

            return mem.ToArray();
        }


        public EstFile( FileResource file )
        {
            File = file;
            File.Reader.BaseStream.Seek( 0, SeekOrigin.Begin );
            NumEntries = File.Reader.ReadUInt32();

            var currentEntryDescOffset = 4;
            var currentEntryOffset     = 4 + EntryDescSize * NumEntries;
            for( var i = 0; i < NumEntries; ++i )
            {
                File.Reader.BaseStream.Seek( currentEntryDescOffset, SeekOrigin.Begin );
                currentEntryDescOffset += EntryDescSize;
                var setId  = File.Reader.ReadUInt16();
                var raceId = File.Reader.ReadUInt16();
                if( !GamePathParser.IdToRace.TryGetValue( raceId, out var genderAndRace ) )
                {
                    continue;
                }

                File.Reader.BaseStream.Seek( currentEntryOffset, SeekOrigin.Begin );
                currentEntryOffset += EntrySize;
                var entry = File.Reader.ReadUInt16();

                AddEntry( genderAndRace.Item1, genderAndRace.Item2, setId, entry );
            }
        }
    }
}