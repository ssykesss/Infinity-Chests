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
        public override string Author => "oli";
        public override string Name => "Infinity Chests";
        public override Version Version => new(2, 0);

        public static bool refillItemsByRegionName = true;
        public static bool refillItemsByChestName = true;
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
            if (TShock.Players[e.Msg.whoAmI] is not TSPlayer player)
                return;
            using var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length));
            switch ((int)e.MsgID)
            {
                case 31:
                    int x = reader.ReadInt16();
                    int y = reader.ReadInt16();
                    int index = Chest.FindChest(x, y);
                    if (index < 0 || index >= 8000)
                        return;
                    if (ChestLock(player, Main.chest[index].name, x, y))
                        e.Handled = true;
                    else
                    {
                        for (int i = 0; i < 40; i++)
                            NetMessage.SendData(32, player.Index, -1, null, index, i);
                        NetMessage.SendData(33, player.Index, -1, null, index);
                        if (player.HasBuildPermission(y, x, false) || player.HasPermission("infchests.edit"))
                            return;
                        e.Handled = true;
                    }
                    return;
                case 32:
                    var chestID = reader.ReadInt16();
                    var slot = reader.ReadByte();
                    var stack = reader.ReadInt16();
                    var prefix = reader.ReadByte();
                    var itemID = reader.ReadInt16();

                    if (chestID < 0 || chestID >= 8000)
                        return;
                    Chest chest = Main.chest[chestID];
                    if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(r => config.regions.Contains(r.Name)) && refillItemsByRegionName || config.chestnames.Contains(chest.name) && refillItemsByChestName)
                    {
                        e.Handled = true;
                        NetMessage.SendData(32, -1, -1, null, chestID, slot);
                    }
                    return;
                case 33:
                    e.Handled = true;
                    return;
            }
        }
        private static bool ChestLock(TSPlayer player, string chestName, int x, int y)
        {
            if (config.chestnames.Contains(chestName))
            {
                if (!refillItemsByChestName)
                    return !player.HasPermission("infchests.ignore");
            }
            if (TShock.Regions.InAreaRegion(x, y).Any(r => config.regions.Contains(r.Name)))
            {
                if (!refillItemsByRegionName)
                    return !player.HasPermission("infchests.ignore");               
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
                if (player == null || player.HasPermission("infchests.edit"))
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
                    e.Player.SendInfoMessage("/chest add <region> - (Добавляет регион в список)");
                    e.Player.SendInfoMessage("/chest del <region> - (Удаляет регион из списка)");
                    e.Player.SendInfoMessage("/chest list <page> - (Список регионов)");
                    e.Player.SendInfoMessage("/chest <on/off> - (Включает/Отключает восстановление предметов в регионах)");
                    e.Player.SendInfoMessage(">> /chest -c <help>");
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
                        switch (e.Parameters[1].ToLower())
                        {
                            case "help":
                                e.Player.SendInfoMessage("/chest -c add <chest name> - (Добавляет имя сундука в список)");
                                e.Player.SendInfoMessage("/chest -c del <chest name> - (Удаляет имя сундука из списка)");
                                e.Player.SendInfoMessage("/chest -c list <page> - (Список имён сундуков)");
                                e.Player.SendInfoMessage("/chest -c <on/off> - (Включает/Отключает восстановление вещей в сундуках с именами из списка)");
                                return;
                            case "add":
                                if (e.Parameters.Count > 2)
                                {
                                    string chestName = e.Parameters[2];
                                    if (config.chestnames.Contains(chestName))
                                        e.Player.SendErrorMessage("Этот сундук уже есть в списке! '{0}'", chestName);
                                    else
                                    {
                                        config.chestnames.Add(chestName);
                                        config.Write();
                                        e.Player.SendSuccessMessage("Сундук успешно добавлен в список! '{0}'", chestName);
                                    }
                                }
                                else
                                    e.Player.SendErrorMessage("Неверный формат! /chest -c add <chest>");
                                return;
                            case "del":
                                if (e.Parameters.Count > 2)
                                {
                                    string chestName = e.Parameters[2];
                                    if (!config.chestnames.Contains(chestName))
                                        e.Player.SendErrorMessage("Этого сундука нет в списке! '{0}'", chestName);
                                    else
                                    {
                                        config.chestnames.Remove(chestName);
                                        config.Write();
                                        e.Player.SendSuccessMessage("Сундук успешно удалён из списка! '{0}'", chestName);
                                    }
                                }
                                else
                                    e.Player.SendErrorMessage("Неверный формат! /chest -c del <chest>");
                                return;
                            case "list":
                                if (!PaginationTools.TryParsePageNumber(e.Parameters, 2, e.Player, out int page))
                                    return;
                                IEnumerable<string> strings = from chests in config.chestnames select chests;
                                PaginationTools.SendPage(e.Player, page, PaginationTools.BuildLinesFromTerms(strings, maxCharsPerLine: 75), new()
                                {
                                    HeaderFormat = "Список имён сундуков с восстановлением предметов",
                                    FooterFormat = "Следующая страница /chest -с list {0}",
                                    NothingToDisplayString = "Нет сундуков в списке.",
                                });
                                return;
                            case "on":
                                refillItemsByChestName = true;
                                e.Player.SendSuccessMessage("Восстановление вещей от имени сундуков: Включенно.");
                                return;
                            case "off":
                                refillItemsByChestName = false;
                                NetMessage.SendData(33, -1, e.Player.Index, null, -1);
                                e.Player.SendSuccessMessage("Восстановление вещей от имени сундуков: Отключенно.");
                                return;
                        }
                    }
                    else
                        goto case "help";
                    return;
                case "off":
                    refillItemsByRegionName = false;
                    NetMessage.SendData(33, -1, e.Player.Index, null, -1);
                    e.Player.SendSuccessMessage("Восстановление вещей сундуков в регионах: Отключенно.");
                    return;
                case "on":
                    refillItemsByRegionName = true;
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
                TShock.Log.ConsoleError($"[Infinity Chests] Config reload error: {ex.Message}");
            }
        }
    }
}