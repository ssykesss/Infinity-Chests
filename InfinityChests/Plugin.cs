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
        public string SavePath => Path.Combine(TShock.SavePath, "InfinityChests.json");
        public Config config;

        public override string Author => "Lord Diogen & Raiden";
        public override string Description => "TShock plugin for Terraria Minigames.";
        public override string Name => "Infinity Chests";
        public override Version Version => new Version(1, 2);
        public bool ChestRefill = true;
        public bool ChestRefillByName = true;

        public Plugin(Main game) : base(game) { }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("infchests", ChestCommands, "chestrefill", "cr"));
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.NetSendData.Register(this, OnSendData);
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            GeneralHooks.ReloadEvent += OnReload;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnInitialize(EventArgs args)
        {
            try
            {
                string path = Path.Combine(TShock.SavePath, "InfinityChests.json");
                config = Config.Read(path);
            }
            catch (Exception ex)
            {
                config = new Config();
                TShock.Log.ConsoleError("{0}".SFormat(ex.ToString()));
            }
        }

        public void OnGetData(GetDataEventArgs e)
        {
            var player = TShock.Players[e.Msg.whoAmI];
            if (player == null)
                return;

            if (e.MsgID == PacketTypes.ChestItem)
            {
                using (var r = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    var chestID = r.ReadInt16();
                    var slot = r.ReadByte();
                    var stack = r.ReadInt16();
                    var prefix = r.ReadByte();
                    var itemID = r.ReadInt16();

                    if (chestID < 0 || chestID >= 8000)
                        return;
                    string currentChest = "";
                    string currentRegion = "";
                    Chest chest = Main.chest[chestID];
                    string defaultChestName = chest.name;

                    foreach (string curChest in config.chestnames)
                    {
                        currentChest = curChest;
                        if (currentChest == chest.name)
                            break;
                    }
                    foreach (string curRegion in config.regions)
                    {
                        currentRegion = curRegion;
                        if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(i => i.Name == currentRegion))
                            break;
                    }

                    if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(i => i.Name == currentRegion) && ChestRefill && chest.name == defaultChestName || chest.name == currentChest && ChestRefillByName)
                    {
                        e.Handled = true;
                        Item old = chest.item[slot];
                        NetMessage.SendData(32, -1, -1, null, chestID, slot, old.stack, old.prefix, old.type);
                    }
                }
            }
            else if (e.MsgID == PacketTypes.ChestGetContents)
            {
                using (var r = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    int X = r.ReadInt16();
                    int Y = r.ReadInt16();
                    int index = Chest.FindChest(X, Y);
                    Chest chest = Main.chest[index];

                    string currentChest = "";
                    string currentRegion = "";
                    foreach (string curChest in config.chestnames)
                    {
                        currentChest = curChest;
                        if (currentChest == chest.name)
                            break;
                    }
                    foreach (string curRegion in config.regions)
                    {
                        currentRegion = curRegion;
                        if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(i => i.Name.Contains(currentRegion)))
                            break;
                    }

                    if (ChestRefillDisabled(player, chest, currentRegion, currentChest, X, Y))
                    {
                        player.SendErrorMessage("Chest refill disabled.");
                        e.Handled = true;
                        return;
                    }
                    if (index <= -1)
                        return;

                    for (int i = 0; i < 40; i++)
                        NetMessage.SendData((int)PacketTypes.ChestItem, player.Index, -1, null, index, i);
                    NetMessage.SendData((int)PacketTypes.ChestOpen, player.Index, -1, null, index);
                    if (player.HasBuildPermission(X, Y, false))
                        return;
                    e.Handled = true;
                }
            }
            else if (e.MsgID == PacketTypes.ChestOpen)
            {
                int chestID = player.ActiveChest;
                var chest = Main.chest[chestID];
                int x = chest.x;
                int y = chest.y;
                if (!player.HasBuildPermission(x, y, false))
                    e.Handled = true;
            }
        }

        public bool ChestRefillDisabled(TSPlayer player, Chest chest, string curRegionName, string curChestName, int x, int y)
        {
            if (!player.HasPermission("infchests") && !ChestRefill && TShock.Regions.InAreaRegion(x, y).Any(i => i.Name.Contains(curRegionName) && chest.name != curChestName))
                return true;
            else if (!player.HasPermission("infchests") && !ChestRefillByName && chest.name == curChestName)
                return true;
            return false;
        }

        public void OnSendData(SendDataEventArgs e)
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

        public void ChestCommands(CommandArgs args)
        {
            #region Chest refill by region name
            if (args.Parameters.Count == 0)
            {
                args.Player.SendInfoMessage("Chest refill parameters:" +
                    "\n/cr add <string> *Adds region name into config." +
                    "\n/cr del <string from config (/cr list)> *Remove region name from config." +
                    "\n/cr -s *turn (on/off) chest refill by region name" +
                    "\n/cr list <page> *List of region names.");
                return;
            }
            else if (args.Parameters[0] == "add")
            {
                if (args.Parameters.Count == 1)
                {
                    args.Player.SendErrorMessage("Invalid syntax! /cr add <string>");
                    return;
                }
                string regionname = args.Parameters[1];
                if (regionname.Length != 0 && !config.regions.Contains(regionname))
                {
                    config.regions.Add(regionname);
                    config.Write(SavePath);
                }
                args.Player.SendSuccessMessage("Region was successfully added to the refill function: {0}", regionname);
            }
            else if (args.Parameters[0] == "del")
            {
                string regionname = args.Parameters[1];
                if (args.Parameters.Count == 1)
                {
                    args.Player.SendErrorMessage("Invalid syntax! /cr del <string from config (/cr list <page>)>");
                    return;
                }
                else if (regionname.Length != 0)
                {
                        
                    if (!config.regions.Contains(regionname))
                    {
                        args.Player.SendErrorMessage("Invalid region name! /cr list <page>");
                        return;
                    }
                    config.regions.Remove(regionname);
                    config.Write(SavePath);
                    args.Player.SendSuccessMessage("Region was successfully deleted from the refill function: {0}", regionname);
                }
            }
            else if (args.Parameters[0] == "list")
            {
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out int pageNumber))
                    return;
                IEnumerable<string> strings = (IEnumerable<string>)(from regions in config.regions select regions);
                PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(strings, maxCharsPerLine: 75),
                new PaginationTools.Settings
                {
                    FooterFormat = "List of regions for chest refill.",
                    NothingToDisplayString = "There are currently no regions in the config."
                });
            }
            else if (args.Parameters[0] == "-s")
            {
                ChestRefill = !ChestRefill;
                if (!ChestRefill)
                {
                    foreach (var players in TShock.Players)
                    {
                        if (players == null)
                            continue;
                        NetMessage.SendData((int)PacketTypes.SyncPlayerChestIndex, -1, -1, NetworkText.Empty, players.Index, -1);
                    }
                }
                args.Player.SendInfoMessage($"[c/fffff:Chest refill now is] {((ChestRefill) ? "[c/00E019:Enabled]" : "[c/DD4647:Disabled]")}");
                return;
            }
            #endregion

            #region Chest refill by chest name
            if (args.Parameters[0] == "-c" && args.Parameters.Count == 1)
            {
                args.Player.SendInfoMessage("Chest refill parameters:" +
                    "\n/cr -c add <string> *Adds chest name into config." +
                    "\n/cr -c del <string from config (/cr -c list)> *Remove chest name from config." +
                    "\n/cr -c -s *turn (on/off) chest refill by name" +
                    "\n/cr -c -list <page> *List of chest names.");
                return;
            }
            else if (args.Parameters[0] == "-c" && args.Parameters[1] == "-s")
            {
                ChestRefillByName = !ChestRefillByName;
                if (!ChestRefillByName)
                {
                    foreach (var players in TShock.Players)
                    {
                        if (players == null)
                            continue;
                        NetMessage.SendData((int)PacketTypes.SyncPlayerChestIndex, -1, -1, NetworkText.Empty, players.Index, -1);
                    }
                }
                args.Player.SendInfoMessage($"[c/fffff:Chest refill by name now is] {(ChestRefillByName ? "[c/00E019:Enabled]" : "[c/DD4647:Disabled]")}");
            }
            else if (args.Parameters[0] == "-c" && args.Parameters[1] == "add")
            {
                if (args.Parameters.Count < 3)
                {
                    args.Player.SendErrorMessage("Invalid syntax! /cr -c add <string>");
                    return;
                }
                var chestName = args.Parameters[2];
                if (chestName.Length != 0 && !config.chestnames.Contains(chestName))
                {
                    config.chestnames.Add(chestName);
                    config.Write(SavePath);
                }
                args.Player.SendSuccessMessage("Chest was successfully added to the refill function: {0}", args.Parameters[2]);
            }
            else if (args.Parameters[0] == "-c" && args.Parameters[1] == "del")
            {
                if (args.Parameters.Count < 3)
                {
                    args.Player.SendErrorMessage("Invalid Syntax! /cr -c del <string>");
                    return;
                }
                var chestName = args.Parameters[2];
                if (!config.chestnames.Contains(chestName))
                {
                    args.Player.SendErrorMessage("Invalid chest name! /cr -c list");
                    return;
                }
                else if (args.Parameters[2].Length != 0)
                {
                    config.chestnames.Remove(args.Parameters[2]);
                    config.Write(SavePath);
                    args.Player.SendSuccessMessage("Chest was successfully removed from the refill function: {0}", args.Parameters[2]);
                }
            }
            else if (args.Parameters[0] == "-c" && args.Parameters[1] == "list")
            {
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out int pageNumber))
                    return;
                IEnumerable<string> strings = (IEnumerable<string>)(from chests in config.chestnames select chests);
                PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(strings, maxCharsPerLine: 75),
                new PaginationTools.Settings
                {
                    FooterFormat = "List of chest names for chest refill.",
                    NothingToDisplayString = "There are currently no chest names in the config."
                });
            }
            #endregion
        }

        private void OnReload(ReloadEventArgs e)
        {
            try
            {
                config = Config.Read(SavePath);
                e.Player.SendSuccessMessage("[Infinity Chests] Successfully reloaded config.");
            }
            catch (Exception ex)
            {
                config.Write(SavePath);
                TShock.Log.ConsoleError($"[Infinity Chests] {ex.Message}");
            }
        }
    }
}