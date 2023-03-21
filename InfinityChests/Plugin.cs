using System;
using System.IO;
using System.Linq;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using Terraria;
using Terraria.Localization;

namespace InfinityChests
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public static string ConfigPath { get { return Path.Combine(TShock.SavePath, "InfinityChests.json"); } }
        public Config Config;

        public override string Author => "Lord Diogen";
        public override string Description => "TShock plugin for Terraria Minigames.";
        public override string Name => "Infinity Chests";
        public bool ChestRefill = true;
        public bool ChestRefillByName = true;

        public Plugin(Main game) : base(game) { }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("infchests", ChestCommands, "chestrefill", "cr") { AllowServer = true });
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.NetSendData.Register(this, OnSendData);
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            GeneralHooks.ReloadEvent += GeneralHooks_ReloadEvent;
        }

        private void GeneralHooks_ReloadEvent(ReloadEventArgs e)
        {
            try
            {
                string path = Path.Combine(TShock.SavePath, "InfinityChests.json");
                Config = Config.Read(path);
                foreach (string regions in Config.regions)
                    e.Player.SendSuccessMessage($"Chest refill region name: {regions}");
            }
            catch (Exception ex)
            {
                Config = new Config();
                TShock.Log.ConsoleError("{0}".SFormat(ex.ToString()));
            }
        }
        private void OnInitialize(EventArgs args)
        {
            try
            {
                string path = Path.Combine(TShock.SavePath, "InfinityChests.json");
                Config = Config.Read(path);
            }
            catch (Exception ex)
            {
                Config = new Config();
                TShock.Log.ConsoleError("{0}".SFormat(ex.ToString()));
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                GeneralHooks.ReloadEvent -= GeneralHooks_ReloadEvent;
            }
            base.Dispose(disposing);
        }

        public void OnGetData(GetDataEventArgs e)
        {
            var player = TShock.Players[e.Msg.whoAmI];
            if (player == null)
                return;
            if (ChestRefill == false)
                return;

            #region REFILL BY CHEST NAME
            if (ChestRefillByName)
            {
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

                        Chest chest = Main.chest[chestID];
                        foreach (var chestRefillByName in Config.chestnames)
                        {
                            if (chest.name == chestRefillByName)
                            {
                                e.Handled = true;
                                Item old = chest.item[slot];
                                NetMessage.SendData(32, -1, -1, null, chestID, slot, old.stack, old.prefix, old.type);
                            }
                        }
                    }
                }
            }
            #endregion

            foreach (var regions in Config.regions)
            {
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

                        Chest chest = Main.chest[chestID];
                        if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(i => i.Name.Contains(regions)))
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

                        if (TShock.Regions.InAreaRegion(X, Y).Any(i => i.Name.Contains(regions)))
                        {
                            int index = Chest.FindChest(X, Y);
                            if (index <= -1)
                                return;

                            for (int i = 0; i < 40; i++)
                                NetMessage.TrySendData(32, e.Msg.whoAmI, -1, null, index, i);
                            NetMessage.TrySendData(33, e.Msg.whoAmI, -1, null, index); // 80 пакет?
                        }
                        else if (player.HasBuildPermission(X, Y, false))
                            return;
                        e.Handled = true;
                    }
                }
                else if (e.MsgID == PacketTypes.ChestOpen)
                {
                    using (var r = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        int index = r.ReadInt16();
                        int Y = r.ReadInt16();
                        int X = r.ReadInt16();

                        if (TShock.Regions.InAreaRegion(X, Y).Any(i => i.Name.Contains(regions)))
                            e.Handled = true;
                    }
                }
            }
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
            if (args.Parameters.Count == 0)
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
            string i = args.Parameters.FirstOrDefault();
            switch (i.ToLower())
            {
                case "list-chests":
                    {
                        foreach (string name in Config.chestnames)
                            args.Player.SendInfoMessage("Chest name for refill: <{0}>", name);
                    }
                    break;
                case "list-region":
                    {
                        foreach (string reg in Config.regions)
                            args.Player.SendInfoMessage("Chest refill regions: <{0}>", reg);
                    }
                    break;
                case "chests":
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
                        args.Player.SendInfoMessage($"[c/fffff:Chest refill by name now is] {((ChestRefillByName) ? "[c/00E019:Enabled]" : "[c/DD4647:Disabled]")}");
                    }
                    break;
            }
        }
    }
}
