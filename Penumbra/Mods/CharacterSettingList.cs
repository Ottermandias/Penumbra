using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Plugin;
using Newtonsoft.Json;
using Penumbra.Models;

namespace Penumbra.Mods
{
    public class CharacterSettingList
    {
        private struct SerializerHelper
        {
            public string            CharacterName;
            public CharacterSettings Settings;
        }

        public readonly Dictionary< string, CharacterSettings > CharacterConfigs = new();

        public void RenewFiles( List< ModInfo > allMods )
        {
            foreach( var settings in CharacterConfigs.Values )
            {
                settings.RenewFiles( allMods );
            }
        }

        public static void SaveToFile( string name, CharacterSettings settings, FileInfo filePath )
        {
            try
            {
                var data = JsonConvert.SerializeObject( new SerializerHelper() { CharacterName = name, Settings = settings }
                    , Formatting.Indented );
                File.WriteAllText( filePath.FullName, data );
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Could not save character config for {name}:\n{e}" );
            }
        }

        public void AddFromFile( FileInfo filePath )
        {
            try
            {
                var data   = File.ReadAllText( filePath.FullName );
                var helper = JsonConvert.DeserializeObject< SerializerHelper >( data );
                if( !CharacterConfigs.ContainsKey( helper.CharacterName ) )
                {
                    CharacterConfigs[ helper.CharacterName ] = helper.Settings;
                }
                else
                {
                    PluginLog.Error( $"Trying to load multiple configs for character {helper.CharacterName}. Using only the first." );
                }
            }
            catch( Exception e )
            {
                PluginLog.Error( $"Error while reading character config {filePath.FullName}:\n{e}" );
            }
        }

        public void ReadAll( DirectoryInfo baseDir )
        {
            foreach( var file in baseDir.EnumerateFiles( "*.json" )
                .Where( f => f.Name.StartsWith( "charconfig" ) ) )
            {
                AddFromFile( file );
            }
        }
    }
}
