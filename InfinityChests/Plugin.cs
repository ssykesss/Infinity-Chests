using System.IO;
using System.Linq;
using Terraria;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;

namespace InfinityChests
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Author => "Lord Diogen";
        public override string Description => "TShock plugin for Terraria Minigames.";
        public override string Name => "Infinity Chests";

        public Plugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.NetSendData.Register(this, OnSendData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.NetSendData.Deregister(this, OnSendData);
            }
            base.Dispose(disposing);
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

                    Chest chest = Main.chest[chestID];
                    if (TShock.Regions.InAreaRegion(chest.x, chest.y).Any(i => i.Name.Contains("Chests")))
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

                    if (TShock.Regions.InAreaRegion(X, Y).Any(i => i.Name.Contains("Chests")))
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

                    if (TShock.Regions.InAreaRegion(X, Y).Any(i => i.Name.Contains("Chests")))
                        e.Handled = true;
                }
            }
        }

        void OnSendData(SendDataEventArgs e)
        {
            if (e.MsgId == PacketTypes.ChestName)
            {
                if (e.ignoreClient == -1 || e.text != null)
                    return;

                var player = TShock.Players[e.ignoreClient];
                if (player == null || player.HasPermission("minigames.staff"))
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
    }
}
