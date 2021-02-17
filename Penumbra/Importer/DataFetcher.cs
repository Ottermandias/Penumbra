using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using Penumbra.Game;
using Penumbra.Models;
using Penumbra.Util;
using Race = Penumbra.Game.Race;

namespace Penumbra.Importer
{
    public class DataFetcher
    {
        private const string TmpDirectory           = "tmp";
        private const string FilesDirectory         = "files";
        private const string EquipmentParameters    = "chara/xls/equipmentparameter/equipmentparameter.eqp";
        private const string GimmickParameter       = "chara/xls/equipmentparameter/gimmickparameter.gmp";
        private const string DeformationParametersE = "chara/xls/charadb/equipmentdeformerparameter/c";
        private const string DeformationParametersA = "chara/xls/charadb/accessorydeformerparameter/c";
        private const string DeformationExt         = ".eqdp";
        private const string ExtraSkeletonHead      = "chara/xls/charadb/extra_met.est";
        private const string ExtraSkeletonBody      = "chara/xls/charadb/extra_top.est";
        private const string ExtraSkeletonHair      = "chara/xls/charadb/hairskeletontemplate.est";
        private const string ExtraSkeletonFace      = "chara/xls/charadb/faceskeletontemplate.est";

        private readonly DalamudPluginInterface _pi;
        private readonly DirectoryInfo          _dir;

        private static string DeformationForRace( string raceCode, bool accessory )
            => $"{( accessory ? DeformationParametersA : DeformationParametersE )}{raceCode}{DeformationExt}";

        private static string DeformationForRace( Gender gender, Race race, bool accessory )
            => DeformationForRace( GamePathParser.RaceToIdString[(gender, race)], accessory );

        private void CreateTmpDir()
        {
            DirectoryInfo dir = new( Path.Combine( _dir.FullName, FilesDirectory ) );
            if( dir.Exists )
            {
                return;
            }

            try
            {
                Directory.CreateDirectory( dir.FullName );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not create temporary mod directory at {dir.FullName}:\n{e}" );
            }
        }

        private static void Parse( FileResource file )
        {
            try
            {
                PluginLog.Information( $"Offset {file.FileInfo.Offset}" );
                var br         = file.Reader;
                var blockCount = br.ReadInt32();
                PluginLog.Information( $"Block Count : {blockCount}" );
                for( var i = 0; i < blockCount; ++i )
                {
                    br.BaseStream.Seek( 4 + 8 * i, SeekOrigin.Begin );
                    var blockOffset = br.ReadInt32();
                    br.BaseStream.Seek( blockOffset, SeekOrigin.Begin );
                    br.ReadBytes( 8 );
                    var compressedSize   = br.ReadInt32();
                    var uncompressedSize = br.ReadInt32();
                    var uncompressed     = compressedSize == 32000;
                    PluginLog.Information(
                        $"Block {i}: Offset {blockOffset}, compressed size {compressedSize}, uncompressedSize {uncompressedSize}" );
                }
            }
            catch( Exception e )
            { }
        }

        private void WriteDefaultFile( Option option, string which )
        {
            FileInfo tmp = new( which );
            PluginLog.Information( $"{tmp.Name}" );
            var path = tmp.Name[ 0 ] == 'c' && tmp.FullName.Contains( "accessory" )
                ? new FileInfo( Path.Combine( _dir.FullName, FilesDirectory, "a" + tmp.Name ) )
                : new FileInfo( Path.Combine( _dir.FullName, FilesDirectory, tmp.Name ) );
            var file = FetchFile( which );
            option.AddFile( new RelPath( path, _dir ), new GamePath( which ) );
            if( file == null )
            {
                return;
            }

            if( path.Extension == ".eqp" || path.Extension == ".gmp" )
            {
                var eqpFile = new EqpFile( file );
                File.WriteAllBytes(path.FullName + "b", eqpFile.WriteBytes()  );
            }

            if( path.Extension == ".est" )
            {
                var estFile = new EstFile( file );
                File.WriteAllBytes(path.FullName + "b", estFile.WriteBytes()  );
            }

            File.WriteAllBytes( path.FullName, file.Data );
            Parse( file );
        }

        private void WriteEqdpFile( Option option, string which, bool accessory )
        {
            FileInfo tmp = new( which );
            PluginLog.Information( $"{tmp.Name}" );
            var path = accessory
                ? new FileInfo( Path.Combine( _dir.FullName, FilesDirectory, "a" + tmp.Name ) )
                : new FileInfo( Path.Combine( _dir.FullName, FilesDirectory, tmp.Name ) );
            var file = FetchFile( which );
            if( file == null )
            {
                return;
            }
            var eqdp = new EqdpFile( file );

            option.AddFile( new RelPath( path, _dir ), new GamePath( which ) );

            File.WriteAllBytes( path.FullName, file.Data );
            File.WriteAllBytes( path.FullName + "b", eqdp.WriteBytes() );
            Parse( file );
        }

        private void WriteDefaultFiles()
        {
            var meta = new ModMeta
            {
                Name   = "Required",
                Author = "Penumbra",
                Groups = new Dictionary< string, InstallerInfo >()
                {
                    {
                        "Required", new()
                        {
                            GroupName     = "Required",
                            SelectionType = SelectType.Single,
                            Options = new List< Option >()
                            {
                                new()
                                {
                                    OptionName  = "Required",
                                    OptionFiles = new Dictionary< RelPath, HashSet< GamePath > >()
                                }
                            }
                        }
                    }
                }
            };
            var option = meta.Groups[ "Required" ].Options.FirstOrDefault( O => O.OptionName == "Required" );

            WriteDefaultFile( option, EquipmentParameters );
            WriteDefaultFile( option, GimmickParameter );
            WriteDefaultFile( option, ExtraSkeletonHead );
            WriteDefaultFile( option, ExtraSkeletonBody );
            WriteDefaultFile( option, ExtraSkeletonHair );
            WriteDefaultFile( option, ExtraSkeletonFace );
            WriteEqdpFile( option, DeformationForRace( Gender.Female, Race.Midlander, false ), false );

            foreach (var (gender, race) in GamePathParser.IdStringToRace.Values)
            {
                WriteEqdpFile( option, DeformationForRace( gender, race, true ), true );
                WriteEqdpFile( option, DeformationForRace( gender, race, false ), false );
            }

            File.WriteAllText(
                Path.Combine( _dir.FullName, "meta.json" ),
                JsonConvert.SerializeObject( meta, Formatting.Indented )
            );
        }

        public DataFetcher( DalamudPluginInterface pi, DirectoryInfo modCollection )
        {
            _pi  = pi;
            _dir = new DirectoryInfo( Path.Combine( modCollection.FullName, TmpDirectory ) );
            CreateTmpDir();
            var max = _pi.Data.Excel.GetSheet< Item >().Max( Row => Row.ModelSub & 0xFFFF );
            PluginLog.Information( $"Current Max Item {max}" );
            WriteDefaultFiles();
        }

        private Lumina.Data.Files.ImcFile GetImcFile( string path )
            => _pi.Data.GetFile< Lumina.Data.Files.ImcFile >( path );

        private FileResource FetchFile( string name )
            => _pi.Data.GetFile( name );
    }
}