using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Penumbra.Util;

namespace Penumbra.Game
{
    public class ItemFiller
    {
        private readonly DalamudPluginInterface _pi;
        private readonly ExcelSheet< Item >     _items;

        public ItemFiller( DalamudPluginInterface pi )
        {
            _pi    = pi;
            _items = _pi.Data.GetExcelSheet< Item >();
        }

        public string[] RunEquip( IEnumerable< GamePath > iterator )
        {
            var itemInfos = iterator
                .Select( GamePathParser.GetFileInfo )
                .Where( s => s is ItemInfo )
                .ToHashSet();

            if( itemInfos.Count == 0 )
            {
                return new string[] { };
            }

            HashSet< uint > itemIds = new( itemInfos.Count );
            foreach( var item in _items )
            {
                foreach( var info in itemInfos.Where( info => info.CompatibleWith( item ) ) )
                {
                    itemIds.Add( item.RowId );
                    switch( info )
                    {
                        case EquipInfo equipInfo:
                            PluginLog.Information(
                                $"{item.Name} for {Enum.GetName( typeof( Game.Race ), equipInfo.Race )} {Enum.GetName( typeof( Gender ), equipInfo.Gender )}s." );
                            break;
                        case WeaponInfo:
                            PluginLog.Information( $"{item.Name}." );
                            break;
                    }
                }
            }

            return itemIds.Select( i => i.ToString() ).ToArray();
        }
    }
}