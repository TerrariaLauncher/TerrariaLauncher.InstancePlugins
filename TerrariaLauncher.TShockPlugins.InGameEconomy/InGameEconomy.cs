using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TerrariaLauncher.TShockPlugins.InGameEconomy
{
    [TerrariaApi.Server.ApiVersion(2, 1)]
    public class InGameEconomy : TerrariaApi.Server.TerrariaPlugin
    {
        public InGameEconomy(Terraria.Main game) : base(game)
        {
        }

        public override string Name => "In-Game Economy";
        public override Version Version => new Version(1, 0, 0);
        public override string Author => "Terraria Launcher";
        public override string Description => "Economy for Terraria.";
        public override string UpdateURL => "https://github.com/TerrariaLauncher";

        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        private Dictionary<int, BatchBlock<NpcStrikeEventDetails>> npcStrikeEventBatchs;
        private ActionBlock<NpcStrikeEventDetails[]> npcStrikeBatchHandler;

        private async Task HandleNpcStrikeBatch(NpcStrikeEventDetails[] batch)
        {
            if (batch.Length <= 0) return;

            var groupingStrikesByNpcObject = batch.GroupBy(
                (strike) => strike.Npc.NpcObject,
                (strike) => strike,
                (key, group) => new { NpcObject = key, Strikes = group },
                new TerrariaNpcEqualityComparer()
            );

            if (groupingStrikesByNpcObject.Count() > 1)
            {
                // "There are two NPC types in a batch!"
                // For example, when strike "Eater of Worlds". Need a investigation.
            }

            foreach (var group in groupingStrikesByNpcObject)
            {
                Dictionary<int, double> damagesByUser = new Dictionary<int, double>();
                foreach (var strike in group.Strikes)
                {
                    if (!strike.Player.IsLoggedIn)
                        continue;
                    if (!strike.Npc.Active)
                        continue;

                    if (!damagesByUser.ContainsKey(strike.Player.User.Id))
                    {
                        damagesByUser[strike.Player.User.Id] = 0.0d;
                    }

                    double damage = 0.0d;
                    if (strike.Npc.CurrentLife > 0)
                    {
                        damage = (strike.Critical ? 2 : 1) * Terraria.Main.CalculateDamageNPCsTake(strike.Damage, strike.Npc.CurrentDefense);
                        if (damage > strike.Npc.CurrentLife)
                        {
                            damage = strike.Npc.CurrentLife;
                        }
                    }
                    else
                    {
                        damage = 0;
                    }
                    damagesByUser[strike.Player.User.Id] += damage;
                }

                foreach (var item in damagesByUser)
                {
                    var userId = item.Key;
                    var damages = item.Value;

                    var who = TShockAPI.TShock.Players.FirstOrDefault((player) => player != null && player.IsLoggedIn && player.Account?.ID == userId);
                    who?.SendInfoMessage($"You gained {damages} damage(s) from {group.NpcObject.FullName}");
                }
            }

            await Task.CompletedTask;
        }

        public override void Initialize()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.npcStrikeBatchHandler = new ActionBlock<NpcStrikeEventDetails[]>(this.HandleNpcStrikeBatch);
            this.npcStrikeEventBatchs = new Dictionary<int, BatchBlock<NpcStrikeEventDetails>>();
            for (int i = 0; i < Terraria.Main.npc.Length; ++i)
            {
                var batch = new BatchBlock<NpcStrikeEventDetails>(1024);
                batch.LinkTo(this.npcStrikeBatchHandler);
                this.npcStrikeEventBatchs.Add(i, batch);
            }

            TerrariaApi.Server.ServerApi.Hooks.NetSendData.Register(this, this.HandleNetSendData);
            TerrariaApi.Server.ServerApi.Hooks.NetSendBytes.Register(this, this.HandleNetSendBytes);
            TerrariaApi.Server.ServerApi.Hooks.NetGetData.Register(this, this.HandleNetGetData);

            TerrariaApi.Server.ServerApi.Hooks.NpcSpawn.Register(this, this.HandleNpcSpawn);
            TerrariaApi.Server.ServerApi.Hooks.NpcStrike.Register(this, this.HandleNpcStrike);
            TerrariaApi.Server.ServerApi.Hooks.NpcKilled.Register(this, this.HandleNpcKilled);
            TerrariaApi.Server.ServerApi.Hooks.NpcLootDrop.Register(this, this.HandleNpcLootDrop);

            // Don't use "TerrariaApi.Server.ServerApi.Hooks.NpcKilled" because this does not trigger when NPC despawn.

            TShockAPI.GetDataHandlers.PlayerDamage.Register(this.HandlePlayerDamage);
            // TShockAPI.TSPlayer.All.SendData(,,,,,,,);
            // Terraria.NetMessage.SendData()
        }

        private void HandleNetSendBytes(TerrariaApi.Server.SendBytesEventArgs args)
        {
            /*
            var msgId = args.Buffer[args.Offset + 2];
            switch ((PacketTypes)msgId)
            {
                case PacketTypes.NpcUpdate:
                    var buffer = new byte[args.Count - 3];
                    System.Array.Copy(args.Buffer, args.Offset + 3, buffer, 0, args.Count - 3);
                    using (var stream = new System.IO.MemoryStream(buffer))
                    {
                        using (var reader = new System.IO.BinaryReader(stream))
                        {
                            Int16 npcId = reader.ReadInt16();
                            reader.BaseStream.Seek(16, System.IO.SeekOrigin.Current);
                            UInt16 playerId = reader.ReadUInt16();
                            TShockAPI.TShock.Log.ConsoleInfo("NPC Update: {0} {1}", npcId, playerId);
                        }
                    }
                    break;
                default:
                    return;
            }
            */
        }

        private void HandleNetSendData(TerrariaApi.Server.SendDataEventArgs args)
        {
            switch (args.MsgId)
            {
                // case PacketTypes.NpcStrike: Don't known specfied player who damaged the NPC. You 
                case PacketTypes.NpcUpdate:
                    var npcId = args.number;
                    // TShockAPI.TShock.Log.ConsoleInfo("{0} {1} {2} {3} {4} {5} {6}", args.MsgId, args.number, args.ignoreClient, args.remoteClient, Terraria.Main.npc[args.number].FullName, Terraria.Main.npc[args.number].life, Terraria.Main.npc[args.number].active);
                    if (!Terraria.Main.npc[npcId].active)
                    {
                        this.npcStrikeEventBatchs[npcId].TriggerBatch();
                    }
                    break;
            }
        }

        private void HandleNetGetData(TerrariaApi.Server.GetDataEventArgs args)
        {
            switch (args.MsgID)
            {
                // case PacketTypes.PlayerHurtV2: // When player is attacked.
                // case PacketTypes.PlayerDeathV2: // When player death.
                // case PacketTypes.NotifyPlayerNpcKilled:
                case PacketTypes.NpcStrike:
                    {
                        if (args.Handled) return;

                        var buffer = new byte[args.Length];
                        System.Array.Copy(args.Msg.readBuffer, args.Index, buffer, 0, args.Length);

                        Int16 npcId;
                        Int16 damage;
                        Single knockBack;
                        Byte hitDirection;
                        Boolean critical;

                        using (var stream = new System.IO.MemoryStream(buffer))
                        {
                            using (var streamReader = new System.IO.BinaryReader(stream))
                            {
                                npcId = streamReader.ReadInt16();
                                damage = streamReader.ReadInt16();
                                knockBack = streamReader.ReadSingle();
                                hitDirection = streamReader.ReadByte();
                                critical = streamReader.ReadBoolean();
                            }
                        }

                        var npc = Terraria.Main.npc[npcId];
                        var player = TShockAPI.TShock.Players[args.Msg.whoAmI];

                        this.npcStrikeEventBatchs[npcId].Post(new NpcStrikeEventDetails()
                        {
                            Damage = damage,
                            HitDirection = hitDirection,
                            Critical = critical,
                            KnockBack = knockBack,
                            Npc = new NpcStrikeEventDetails.Types.Npc()
                            {
                                Id = npcId,
                                Type = npc.type,
                                CurrentLife = npc.life,
                                CurrentDefense = npc.defense,
                                Active = npc.active,
                                NpcObject = npc
                            },
                            Player = new NpcStrikeEventDetails.Types.Player()
                            {
                                Id = player.Index,
                                Name = player.Name,
                                IsLoggedIn = player.IsLoggedIn,
                                TSPlayerObject = player,
                                User = new NpcStrikeEventDetails.Types.Player.Types.User()
                                {
                                    Id = player.Account?.ID ?? -1,
                                    Name = player.Account?.Name ?? ""
                                },
                                Group = new NpcStrikeEventDetails.Types.Player.Types.Group()
                                {
                                    Name = player.Group?.Name ?? ""
                                }
                            }
                        });
                    }
                    break;
            }
        }

        private void HandleNpcSpawn(TerrariaApi.Server.NpcSpawnEventArgs args)
        {

        }

        private void HandleNpcStrike(TerrariaApi.Server.NpcStrikeEventArgs args)
        {
            // TShockAPI.TShock.Log.ConsoleInfo("NPC Strike: {0} {1} {2} {3}", args.Npc.type, args.Npc.TypeName, args.Damage, args.Npc.life);
        }

        private void HandleNpcKilled(TerrariaApi.Server.NpcKilledEventArgs args)
        {
            // TShockAPI.TShock.Log.ConsoleInfo("NPC Killed: {0} {1}", args.npc.type, args.npc.TypeName);
        }

        private void HandleNpcLootDrop(TerrariaApi.Server.NpcLootDropEventArgs args)
        {
            
        }

        private void HandlePlayerDamage(object sender, TShockAPI.GetDataHandlers.PlayerDamageEventArgs args)
        {

        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TerrariaApi.Server.ServerApi.Hooks.NetSendData.Deregister(this, this.HandleNetSendData);
                TerrariaApi.Server.ServerApi.Hooks.NetSendBytes.Register(this, this.HandleNetSendBytes);
                TerrariaApi.Server.ServerApi.Hooks.NetGetData.Deregister(this, this.HandleNetGetData);

                TerrariaApi.Server.ServerApi.Hooks.NpcSpawn.Deregister(this, this.HandleNpcSpawn);
                TerrariaApi.Server.ServerApi.Hooks.NpcStrike.Deregister(this, this.HandleNpcStrike);
                TerrariaApi.Server.ServerApi.Hooks.NpcKilled.Deregister(this, this.HandleNpcKilled);
                TerrariaApi.Server.ServerApi.Hooks.NpcLootDrop.Deregister(this, this.HandleNpcLootDrop);
            }

            base.Dispose(disposing);
        }
    }

    public class NpcStrikeEventDetails
    {
        public bool Critical { get; set; }
        public int Damage { get; set; }
        public float KnockBack { get; set; }
        public int HitDirection { get; set; }
        public bool NoEffect { get; set; }

        public Types.Player Player { get; set; }

        public Types.Npc Npc { get; set; }

        public class Types
        {
            public class Player
            {
                public int Id { get; set; }
                public string Name { get; set; }
                public bool IsLoggedIn { get; set; }
                public TShockAPI.TSPlayer TSPlayerObject { get; set; }
                public Types.User User { get; set; }
                public Types.Group Group { get; set; }

                public class Types
                {
                    public class User
                    {
                        public int Id { get; set; }
                        public string Name { get; set; }
                    }

                    public class Group
                    {
                        public string Name { get; set; }
                    }
                }
            }

            public class Npc
            {
                public int Id { get; set; }
                public int Type { get; set; }
                public int CurrentLife { get; set; }
                public int CurrentDefense { get; set; }
                public bool Active { get; set; }
                public Terraria.NPC NpcObject { get; set; }
            }
        }
    }

    public class TerrariaNpcEqualityComparer : IEqualityComparer<Terraria.NPC>
    {
        public bool Equals(Terraria.NPC x, Terraria.NPC y)
        {
            return Object.ReferenceEquals(x, y);
        }

        public int GetHashCode(Terraria.NPC obj)
        {
            return obj.GetHashCode();
        }
    }
}
