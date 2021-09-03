using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace TerrariaLauncher.TShockPlugins.TradingSystemAgent.InGameEconomy.NpcDamageTracking
{
    public class NpcDamageTracking: IDisposable
    {
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;

        private BufferBlock<(Terraria.NPC npc, NpcStrike[] strikes)> npcStrikesBuffer;
        private ActionBlock<(Terraria.NPC npc, NpcStrike[] strikes)> npcStrikesHandler;

        private HashSet<Terraria.NPC> currentNpcs;
        private ConcurrentDictionary<Terraria.NPC, ConcurrentBag<NpcStrike>> npcStrikesBackLog;

        public NpcDamageTracking(IServiceProvider serviceProvider)
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.currentNpcs = new HashSet<Terraria.NPC>();
            this.npcStrikesBackLog = new ConcurrentDictionary<Terraria.NPC, ConcurrentBag<NpcStrike>>();

            this.npcStrikesBuffer = new BufferBlock<(Terraria.NPC npc, NpcStrike[] strikes)>(new DataflowBlockOptions()
            {
                CancellationToken = this.cancellationToken
            });
            this.npcStrikesHandler = new ActionBlock<(Terraria.NPC npc, NpcStrike[] strikes)>(HandleNpcStrikes);
            this.npcStrikesBuffer.LinkTo(npcStrikesHandler);

            var registrator = serviceProvider.GetRequiredService<TradingSystemAgent>();
            TerrariaApi.Server.ServerApi.Hooks.NetSendData.Register(registrator, this.HandleNetSendData);
            TerrariaApi.Server.ServerApi.Hooks.NetGetData.Register(registrator, this.HandleNetGetData);
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

        private bool disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            if (disposing)
            {

            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}

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
