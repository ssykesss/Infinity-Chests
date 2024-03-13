using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace InfinityChests
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Author => "Lord Diogen & !skxdwlker";
        public override string Name => "Infinity Chests";
        public override Version Version => new(2, 0);

        private static bool _refillItemsByRegionName = true;
        private static bool _refillItemsByChestName = true;
        private static Config config = Config.Read();
        public Plugin(Main game) : base(game) { }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("infchests.edit", ChestCommands, "chest", "cr"));
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.NetSendData.Register(this, OnSendData);
            GeneralHooks.ReloadEvent += OnReload;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnGetData(GetDataEventArgs e)
        {
            var player = TShock.Players[e.Msg.whoAmI];
            if (player == null)
                return;
            if (e.MsgID == PacketTypes.ChestItem)
            {
                using var r = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length));
                var chestID = r.ReadInt16();
                var slot = r.ReadByte();
                var stack = r.ReadInt16();
                var prefix = r.ReadByte();
                var itemID = r.ReadInt16();

                if (chestID < 0 || chestID >= 8000)
                    return;
                Chest chest = Main.chest[chestID];
                if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(r => config.regions.Contains(r.Name)) && _refillItemsByRegionName || config.chestnames.Contains(chest.name) && _refillItemsByChestName)
                {
                    e.Handled = true;
                    Item old = chest.item[slot];
                    NetMessage.SendData(32, -1, -1, null, chestID, slot, old.stack, old.prefix, old.type);
                }
            }
            else if (e.MsgID == PacketTypes.ChestGetContents)
            {
                using var r = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length));
                int x = r.ReadInt16();
                int y = r.ReadInt16();
                int index = Chest.FindChest(x, y);
                if (index < 0 || index >= 8000)
                    return;
                if (ChestLock(player, Main.chest[index].name, x, y))
                {
                    player.SendErrorMessage("Chest refill disabled.");
                    e.Handled = true;
                }
                else
                {
                    for (int i = 0; i < 40; i++)
                        NetMessage.SendData(32, player.Index, -1, null, index, i);
                    NetMessage.SendData(33, player.Index, -1, null, index);
                    if (player.HasBuildPermission(y, x, false) || player.HasPermission("infchests.edit"))
                        return;
                    e.Handled = true;
                }
            }
        }
        private static bool ChestLock(TSPlayer player, string chest, int x, int y)
        {
            if (config.chestnames.Contains(chest))
            {
                if (!_refillItemsByChestName)
                {
                    if (player.HasPermission("infchests.ignore"))
                    {
                        return false;
                    }
                    return true;
                }
            }
            else if (TShock.Regions.InAreaRegion(x, y).Any(r => config.regions.Contains(r.Name)))
            {
                if (!_refillItemsByRegionName)
                {
                    if (player.HasPermission("infchests.ignore"))
                    {
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }
        private static void OnSendData(SendDataEventArgs e)
        {
            if (e.MsgId == PacketTypes.ChestName)
            {
                if (e.ignoreClient == -1 || e.text != null)
                    return;

                var player = TShock.Players[e.ignoreClient];
                if (player == null || player.HasPermission("tm.staff"))
                    return;

                Chest chest = Main.chest[e.number];
                string defaultname = chest.name;

                var tile = Main.tile[chest.x, chest.y];
                if (tile.type == 21)
                    defaultname = Language.GetText("LegacyChestType." + (tile.frameX / 36)).Value;
                if (tile.type == 467)
                    defaultname = Language.GetText("LegacyChestType2." + (tile.frameX / 36)).Value;

                if (defaultname != chest.name)
                {
                    chest.name = defaultname;
                    NetMessage.SendData(69, -1, -1, NetworkText.FromLiteral(defaultname), e.number, chest.x, chest.y);
                    e.Handled = true;
                }
            }
        }
        private void ChestCommands(CommandArgs e)
        {
            switch (e.Parameters.Count < 1 ? "help" : e.Parameters[0].ToLower())
            {
                case "help":
                    e.Player.SendInfoMessage("/chest add <region> - Добавить регион в список.");
                    e.Player.SendInfoMessage("/chest del <region> - Удалить регион из списка.");
                    e.Player.SendInfoMessage("/chest list <page> - Список регионов.");
                    e.Player.SendInfoMessage("/chest <on/off> - Включить/Выключить восстановление предметов от региона.");
                    e.Player.SendInfoMessage("/chest -c <add, del, list, on/off> - Восставновление предметов от имени сундука. (Префикс: -c)");
                    return;
                case "add":
                    if (e.Parameters.Count > 1)
                    {
                        string region = e.Parameters[1];
                        if (config.regions.Contains(region))
                        {
                            e.Player.SendErrorMessage("Этот регион уже есть в списке! '{0}'", region);
                        }
                        else if (TShock.Regions.GetRegionByName(region) == null)
                        {
                            e.Player.SendErrorMessage("Региона с таким названием не существует! '{0}'", region);
                        }
                        else
                        {
                            config.regions.Add(region);
                            config.Write();
                            e.Player.SendSuccessMessage("Регион успешно добавлен в список! '{0}'", region);
                        }
                    }
                    else
                    {
                        e.Player.SendErrorMessage("Неверный формат! /chest add <region>");
                    }
                    return;
                case "del":
                    if (e.Parameters.Count > 1)
                    {
                        string region = e.Parameters[1];
                        if (!config.regions.Contains(region))
                        {
                            e.Player.SendErrorMessage("Этого региона нет в списке! '{0}'", region);
                        }
                        else
                        {
                            config.regions.Remove(region);
                            config.Write();
                            e.Player.SendSuccessMessage("Регион успешно удалён из списка! '{0}'", region);
                        }
                    }
                    else
                    {
                        e.Player.SendErrorMessage("Неверный формат! /chest del <region>");
                    }
                    return;
                case "list":
                    {
                        if (!PaginationTools.TryParsePageNumber(e.Parameters, 1, e.Player, out int page))
                            return;
                        IEnumerable<string> strings = from regions in config.regions select regions;
                        PaginationTools.SendPage(e.Player, page, PaginationTools.BuildLinesFromTerms(strings, maxCharsPerLine: 75), new()
                        {
                            HeaderFormat = "Список регионов для восстановления предметов в сундуках.",
                            FooterFormat = "Следующая страница /chest list",
                            NothingToDisplayString = "Нет регионов в списке.",
                        });
                    }
                    return;
                case "-c":
                    if (e.Parameters.Count > 1)
                    {
                        string param = e.Parameters[1].ToLower();
                        if (param == "add")
                        {
                            if (e.Parameters.Count > 2)
                            {
                                string chest = e.Parameters[2];
                                if (config.chestnames.Contains(chest))
                                {
                                    e.Player.SendErrorMessage("Этот сундук уже есть в списке! '{0}'", chest);
                                }
                                else
                                {
                                    config.chestnames.Add(chest);
                                    config.Write();
                                    e.Player.SendSuccessMessage("Сундук успешно добавлен в список! '{0}'", chest);
                                }
                            }
                            else
                            {
                                e.Player.SendErrorMessage("Неверный формат! /chest -c add <chest>");
                            }
                        }
                        else if (param == "del")
                        {
                            if (e.Parameters.Count > 2)
                            {
                                string chest = e.Parameters[2];
                                if (!config.chestnames.Contains(chest))
                                {
                                    e.Player.SendErrorMessage("Этого сундука нет в списке! '{0}'", chest);
                                }
                                else
                                {
                                    config.chestnames.Remove(chest);
                                    config.Write();
                                    e.Player.SendSuccessMessage("Сундук успешно удалён из списка! '{0}'", chest);
                                }
                            }
                            else
                            {
                                e.Player.SendErrorMessage("Неверный формат! /chest -c del <chest>");
                            }
                        }
                        else if (param == "list")
                        {
                            if (!PaginationTools.TryParsePageNumber(e.Parameters, 2, e.Player, out int page))
                                return;
                            IEnumerable<string> strings = from chests in config.chestnames select chests;
                            PaginationTools.SendPage(e.Player, page, PaginationTools.BuildLinesFromTerms(strings, maxCharsPerLine: 75), new()
                            {
                                HeaderFormat = "Список имён сундуков с восстановлением предметов",
                                FooterFormat = "Следующая страница /chest -с list {0}",
                                NothingToDisplayString = "Нет сундуков в списке.",
                            });
                        }
                        else if (param == "on")
                        {
                            _refillItemsByChestName = true;
                            e.Player.SendSuccessMessage("Восстановление вещей от имени сундуков: Включенно.");
                        }
                        else if (param == "off")
                        {
                            _refillItemsByChestName = false;
                            SendChestOpen(e.Player.Index);
                            e.Player.SendSuccessMessage("Восстановление вещей от имени сундуков: Отключенно.");
                        }
                        else
                        {
                            e.Player.SendErrorMessage("Такого параметра не существует! /chest -c <add, del, list, on/off>");
                        }
                    }
                    else
                    {
                        goto case "help";
                    }
                    return;
                case "off":
                    _refillItemsByRegionName = false;
                    SendChestOpen(e.Player.Index);
                    e.Player.SendSuccessMessage("Восстановление вещей сундуков в регионах: Отключенно.");
                    return;
                case "on":
                    _refillItemsByRegionName = true;
                    e.Player.SendSuccessMessage("Восстановление вещей сундуков в регионах: Включенно.");
                    return;
                default:
                    goto case "help";
            }            
        }
        private void OnReload(ReloadEventArgs e)
        {
            try
            {
                config = Config.Read();
                e.Player.SendSuccessMessage("[Infinity Chests] Successfully reloaded config.");
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"[Infinity Chests] {ex.Message}");
            }
        }
        private static void SendChestOpen(int ignoreClient)
        {
            for (int plr = 0; plr < 255; plr++)
            {
                if (plr == ignoreClient)
                {
                    continue;
                }
                NetMessage.SendData(33, plr, -1, null, -1);
            }
        }
    }
}