using System.Collections.Generic;
using System;
using Dalamud.Plugin;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System.Linq;

namespace Penumbra
{

    public class ItemFiller
    {
        private readonly DalamudPluginInterface _pi;
        private readonly ExcelSheet<Item>       _items;

        public ItemFiller(DalamudPluginInterface pi)
        {
            _pi = pi;
            _items = _pi.Data.GetExcelSheet<Item>();
        }

        public string[] RunEquip(IEnumerable<GamePath> iterator)
        {
            var itemInfos = iterator
                .Select(p => GameFiles.GamePathParser.GetFileInfo(p))
                .Where(s => s is GameFiles.ItemInfo)
                .ToHashSet();

            if (itemInfos.Count == 0)
                return new string[0]{ };

            HashSet<uint> itemIds = new(itemInfos.Count);
            foreach (var item in _items)
            {
                foreach (var info in itemInfos)
                {
                    if (info.CompatibleWith(item))
                    {
                        itemIds.Add(item.RowId);
                        if (info is GameFiles.EquipInfo)
                            PluginLog.Information($"{item.Name} for {Enum.GetName(typeof(GameFiles.Race), (info as GameFiles.EquipInfo).Race)} {Enum.GetName(typeof(GameFiles.Gender), (info as GameFiles.EquipInfo).Gender)}s.");
                        else if (info is GameFiles.WeaponInfo)
                            PluginLog.Information($"{item.Name}.");
                    }
                }
            }
            return itemIds.Select( i => i.ToString() ).ToArray();
        }
    }
}
