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

        private ConcurrentDictionary<Terraria.NPC, ConcurrentBag<NpcStrike>> npcStrikesBackLog;
        private HashSet<Terraria.NPC> currentNpcs;

        private BufferBlock<(Terraria.NPC npc, NpcStrike[] strikes)> npcStrikesBuffer;
        private ActionBlock<(Terraria.NPC npc, NpcStrike[] strikes)> npcStrikesHandler;

        private Task HandleNpcStrikes((Terraria.NPC Npc, NpcStrike[] strikes) args)
        {
            var (npc, strikes) = args;

            var damageDistribution = new Dictionary<int, decimal>();

            foreach (var strike in strikes)
            {
                if (strike.User is null) continue;

                if (!damageDistribution.ContainsKey(strike.User.ID))
                {
                    damageDistribution[strike.User.ID] = 0.0M;
                }

                decimal strikeDamage = 0.0M;
                if (strike.CurrentLife > 0)
                {
                    strikeDamage = (strike.Critical ? 2.0M : 1.0M) * Convert.ToDecimal(Terraria.Main.CalculateDamageNPCsTake(strike.Damage, strike.CurrentDefense));
                    if (strikeDamage > strike.CurrentLife)
                    {
                        strikeDamage = strike.CurrentLife;
                    }
                }
                damageDistribution[strike.User.ID] += strikeDamage;
            }

            foreach (var userId in damageDistribution.Keys)
            {
                var damage = damageDistribution[userId];
                var foundPlayer = TShockAPI.TShock.Players.FirstOrDefault((player) => player != null && player.IsLoggedIn && player.Account?.ID == userId);
                foundPlayer?.SendInfoMessage($"You gained {damage} damage(s) from {npc.FullName}.");
            }

            return Task.CompletedTask;
        }

        public override void Initialize()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.npcStrikesBackLog = new ConcurrentDictionary<Terraria.NPC, ConcurrentBag<NpcStrike>>();
            this.currentNpcs = new HashSet<Terraria.NPC>();

            this.npcStrikesBuffer = new BufferBlock<(Terraria.NPC npc, NpcStrike[] strikes)>(new DataflowBlockOptions()
            {
                CancellationToken = this.cancellationToken
            });
            this.npcStrikesHandler = new ActionBlock<(Terraria.NPC npc, NpcStrike[] strikes)>(HandleNpcStrikes);
            this.npcStrikesBuffer.LinkTo(npcStrikesHandler);

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
                    var npc = Terraria.Main.npc[npcId];
                    // TShockAPI.TShock.Log.ConsoleInfo("{0} {1} {2} {3} {4} {5} {6}", args.MsgId, args.number, args.ignoreClient, args.remoteClient, Terraria.Main.npc[args.number].FullName, Terraria.Main.npc[args.number].life, Terraria.Main.npc[args.number].active);
                    if (npc.active)
                    {
                        var notPresent = this.currentNpcs.Add(npc);
                        if (notPresent)
                        {
                            this.npcStrikesBackLog.TryAdd(npc, new ConcurrentBag<NpcStrike>());
                        }
                    }
                    else
                    {
                        this.currentNpcs.Remove(npc);
                        if (this.npcStrikesBackLog.TryRemove(npc, out var strikes))
                        {
                            if (!this.npcStrikesBuffer.Post((npc, strikes.ToArray())))
                            {
                                TShockAPI.TShock.Log.ConsoleError("Could not post NPC strikes into buffer.");
                            }
                        }
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

                        if (npc.active)
                        {
                            if (this.npcStrikesBackLog.TryGetValue(npc, out var strikes))
                            {
                                strikes.Add(new NpcStrike()
                                {
                                    Damage = damage,
                                    HitDirection = hitDirection,
                                    Critical = critical,
                                    KnockBack = knockBack,

                                    CurrentLife = npc.life,
                                    CurrentDefense = npc.defense,

                                    Player = player,
                                    User = player.Account
                                });
                            }
                        }
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

                this.cancellationTokenSource.Cancel();
                this.cancellationTokenSource.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    public class NpcStrike
    {
        public bool Critical { get; set; }
        public int Damage { get; set; }
        public float KnockBack { get; set; }
        public int HitDirection { get; set; }
        public bool NoEffect { get; set; }

        public int CurrentLife { get; set; }
        public int CurrentDefense { get; set; }

        public TShockAPI.TSPlayer Player { get; set; }
        public TShockAPI.DB.UserAccount User { get; set; }
    }
}
