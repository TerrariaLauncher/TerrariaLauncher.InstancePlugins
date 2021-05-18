using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using TerrariaLauncher.TShockPlugins.TShockManagement.GrpcExtensions;

namespace TerrariaLauncher.TShockPlugins.TShockManagement
{
    [TerrariaApi.Server.ApiVersion(2, 1)]
    public class TShockManagement : TerrariaApi.Server.TerrariaPlugin
    {
        public TShockManagement(Terraria.Main game) : base(game)
        {

        }

        public override string Name => "TShock Management";
        public override Version Version => new Version(1, 0, 0);
        public override string Author => "Terraria Laucher";
        public override string Description => "Remote management the Server.";
        public override string UpdateURL => "https://github.com/TerrariaLauncher";

        private Grpc.Core.Server gRpcServer;

        private BroadcastBlock<Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse> playerSessionEventBroadcaster;
        private BroadcastBlock<Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse> playerDataEventBroadcaster;
        private BroadcastBlock<Protos.InstancePlugins.InstanceManagement.PlayerChatDetails> playerChatBroadcaster;
        private BroadcastBlock<Protos.InstancePlugins.InstanceManagement.ServerBroadcastDetails> serverBroadcastBroadcaster;

        private IServiceCollection serviceCollection;
        private IServiceProvider serviceProvider;

        private CancellationTokenSource tShockCancellationTokenSource;
        private CancellationToken tShockCancellationToken;

        public override void Initialize()
        {
            this.tShockCancellationTokenSource = new CancellationTokenSource();
            this.tShockCancellationToken = this.tShockCancellationTokenSource.Token;

            this.CreateDataflowBlocks();

            this.serviceCollection = new ServiceCollection();
            this.serviceCollection
                .AddSingleton<BroadcastBlock<Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse>>(this.playerSessionEventBroadcaster)
                .AddSingleton<BroadcastBlock<Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse>>(this.playerDataEventBroadcaster)
                .AddSingleton<BroadcastBlock<Protos.InstancePlugins.InstanceManagement.PlayerChatDetails>>(this.playerChatBroadcaster)
                .AddSingleton<BroadcastBlock<Protos.InstancePlugins.InstanceManagement.ServerBroadcastDetails>>(this.serverBroadcastBroadcaster);
            this.serviceProvider = this.serviceCollection.BuildServiceProvider();

            /*
            var reflectionServiceImpl = new Grpc.Reflection.ReflectionServiceImpl(
                TerrariaLauncher.Protos.InstancePlugins.InstanceManagement.TShockUserManagement.Descriptor,
                TerrariaLauncher.Protos.InstancePlugins.InstanceManagement.TShockPlayerManagement.Descriptor,
                TerrariaLauncher.Protos.InstancePlugins.InstanceManagement.TShockGroupManagement.Descriptor
            );
            */

            this.gRpcServer = new Grpc.Core.Server()
            {
                Services =
                {
                    TerrariaLauncher.Protos.InstancePlugins.InstanceManagement.InstanceUserManagement.BindService(
                        new TerrariaLauncher.TShockPlugins.TShockManagement.GrpcServices.TShockUserManagement()
                    ).Intercept(new ScopedServiceProviderInterceptor(this.serviceProvider)),
                    TerrariaLauncher.Protos.InstancePlugins.InstanceManagement.InstancePlayerManagement.BindService(
                        new TerrariaLauncher.TShockPlugins.TShockManagement.GrpcServices.TShockPlayerManagement()
                    ).Intercept(new ScopedServiceProviderInterceptor(this.serviceProvider))
                    // Grpc.Reflection.V1Alpha.ServerReflection.BindService(reflectionServiceImpl)
                },
                Ports =
                {
                    new Grpc.Core.ServerPort("localhost", 3200, Grpc.Core.ServerCredentials.Insecure)
                }
            };
            this.gRpcServer.Start();

            TerrariaApi.Server.ServerApi.Hooks.ServerJoin.Register(this, this.HandlePlayerJoin);
            TerrariaApi.Server.ServerApi.Hooks.ServerLeave.Register(this, this.HandlePlayerLeave);
            TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += this.HandlePlayerPostLogin;
            TShockAPI.Hooks.PlayerHooks.PlayerLogout += this.HandlePlayerLogout;

            TShockAPI.GetDataHandlers.PlayerSlot += this.HandlePlayerInventorySlot;
            TShockAPI.GetDataHandlers.PlayerHP += this.HandlePlayerHealth;
            TShockAPI.GetDataHandlers.PlayerMana += this.HandlePlayerMana;
            TShockAPI.GetDataHandlers.PlayerBuff += this.HandlePlayerBuff;
            TShockAPI.GetDataHandlers.PlayerBuffUpdate += this.HandlePlayerBuffUpdate;

            TShockAPI.Hooks.PlayerHooks.PlayerChat += this.HandlePlayerChat;
            TerrariaApi.Server.ServerApi.Hooks.ServerBroadcast.Register(this, this.HandleServerBroadcast);
            TerrariaApi.Server.ServerApi.Hooks.GamePostInitialize.Register(this, this.HandleGamePostInitialize);
        }

        private void CreateDataflowBlocks()
        {
            this.playerSessionEventBroadcaster = new BroadcastBlock<Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse>(null, new DataflowBlockOptions()
            {
                CancellationToken = this.tShockCancellationToken
            });

            this.playerDataEventBroadcaster = new BroadcastBlock<Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse>(null, new DataflowBlockOptions()
            {
                CancellationToken = this.tShockCancellationToken
            });

            this.playerChatBroadcaster = new BroadcastBlock<Protos.InstancePlugins.InstanceManagement.PlayerChatDetails>(null, new DataflowBlockOptions()
            {
                CancellationToken = this.tShockCancellationToken
            });

            this.serverBroadcastBroadcaster = new BroadcastBlock<Protos.InstancePlugins.InstanceManagement.ServerBroadcastDetails>(null, new DataflowBlockOptions
            {
                CancellationToken = this.tShockCancellationToken
            });
        }

        private void HandleGamePostInitialize(EventArgs args)
        {
            TShockAPI.TShock.Log.ConsoleInfo("Loaded!");
        }

        private void HandlePlayerInventorySlot(object sender, TShockAPI.GetDataHandlers.PlayerSlotEventArgs args)
        {
            if (args.Handled) return;
            var payload = new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse()
            {
                Player = new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse.Types.Player()
                {
                    Id = args.Player.Index,
                    Name = args.Player.Name,
                },
                Details = Google.Protobuf.WellKnownTypes.Any.Pack(
                    new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse.Types.SlotEventDetails()
                    {
                        Slot = args.Slot,
                        Item = args.Type,
                        Prefix = args.Prefix,
                        Stack = args.Stack
                    }),
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };
            this.playerDataEventBroadcaster.Post(payload);
        }

        private void HandlePlayerHealth(object sender, TShockAPI.GetDataHandlers.PlayerHPEventArgs args)
        {
            if (args.Handled) return;
            var payload = new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse()
            {
                Player = new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse.Types.Player()
                {
                    Id = args.Player.Index,
                    Name = args.Player.Name
                },
                Details = Google.Protobuf.WellKnownTypes.Any.Pack(
                    new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse.Types.HealthEventDetails()
                    {
                        Current = args.Current,
                        Base = args.Player.TPlayer.statLifeMax,
                        Max = args.Max
                    }),
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };
            this.playerDataEventBroadcaster.Post(payload);
        }

        private void HandlePlayerMana(object sender, TShockAPI.GetDataHandlers.PlayerManaEventArgs args)
        {
            if (args.Handled) return;
            var payload = new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse()
            {
                Player = new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse.Types.Player()
                {
                    Id = args.Player.Index,
                    Name = args.Player.Name
                },
                Details = Google.Protobuf.WellKnownTypes.Any.Pack(
                    new Protos.InstancePlugins.InstanceManagement.TrackPlayerDataResponse.Types.ManaEventDetails()
                    {
                        Current = args.Current,
                        Base = args.Player.TPlayer.statManaMax,
                        Max = args.Max
                    }),
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };
            this.playerDataEventBroadcaster.Post(payload);
        }

        private void HandlePlayerBuff(object sender, TShockAPI.GetDataHandlers.PlayerBuffEventArgs args)
        {
            // Unknown when this is triggered.
            TShockAPI.TShock.Log.ConsoleInfo("Add Buff: {0} {1} {2}", args.ID, args.Type, args.Time);
        }

        private void HandlePlayerBuffUpdate(object sender, TShockAPI.GetDataHandlers.PlayerBuffUpdateEventArgs args)
        {
            using (var reader = new System.IO.BinaryReader(args.Data))
            {
                for (var i = 0; i < Terraria.Player.maxBuffs; ++i)
                {
                    var buff = reader.ReadUInt16();
                }
                reader.BaseStream.Seek(0, System.IO.SeekOrigin.Begin);
            }
        }

        private Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse MapTShockPlayerToJoinLeaveLoginLogoutEventDetails(
            Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse.Types.EventType eventType, TShockAPI.TSPlayer player)
        {
            return new Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse()
            {
                EventType = eventType,
                Player = new Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse.Types.Player()
                {
                    Id = player.Index,
                    Name = player.Name
                },
                User = new Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse.Types.User()
                {
                    Id = player.Account?.ID ?? -1,
                    Name = player.Account?.Name ?? ""
                },
                Group = new Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse.Types.Group()
                {
                    Name = player.Group?.Name
                },
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };
        }

        private void HandlePlayerJoin(TerrariaApi.Server.JoinEventArgs args)
        {
            if (args.Handled) return;
            var player = TShockAPI.TShock.Players[args.Who];
            this.playerSessionEventBroadcaster.Post(
                MapTShockPlayerToJoinLeaveLoginLogoutEventDetails(
                    Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse.Types.EventType.Join,
                    player
                )
            );
        }

        private void HandlePlayerLeave(TerrariaApi.Server.LeaveEventArgs args)
        {
            var player = TShockAPI.TShock.Players[args.Who];
            this.playerSessionEventBroadcaster.Post(
                MapTShockPlayerToJoinLeaveLoginLogoutEventDetails(
                    Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse.Types.EventType.Leave,
                    player
                )
            );
        }

        private void HandlePlayerPostLogin(TShockAPI.Hooks.PlayerPostLoginEventArgs args)
        {
            this.playerSessionEventBroadcaster.Post(
                MapTShockPlayerToJoinLeaveLoginLogoutEventDetails(
                    Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse.Types.EventType.Login,
                    args.Player
                )
            );
        }

        private void HandlePlayerLogout(TShockAPI.Hooks.PlayerLogoutEventArgs args)
        {
            this.playerSessionEventBroadcaster.Post(
                MapTShockPlayerToJoinLeaveLoginLogoutEventDetails(
                    Protos.InstancePlugins.InstanceManagement.TrackPlayerSessionResponse.Types.EventType.Logout,
                    args.Player
                )
            );
        }

        private void HandlePlayerChat(TShockAPI.Hooks.PlayerChatEventArgs args)
        {
            if (args.Handled) return;
            this.playerChatBroadcaster.Post(new Protos.InstancePlugins.InstanceManagement.PlayerChatDetails()
            {
                Player = new Protos.InstancePlugins.InstanceManagement.PlayerChatDetails.Types.Player()
                {
                    Id = args.Player.Index,
                    Name = args.Player.Name
                },
                RawText = args.RawText,
                FormatedText = args.TShockFormattedText,
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            });
        }

        private void HandleServerBroadcast(TerrariaApi.Server.ServerBroadcastEventArgs args)
        {
            if (args.Handled) return;
            this.serverBroadcastBroadcaster.Post(new Protos.InstancePlugins.InstanceManagement.ServerBroadcastDetails()
            {
                Message = args.Message._text,
                TextColor = new Protos.InstancePlugins.InstanceManagement.ServerBroadcastDetails.Types.TextColor()
                {
                    R = args.Color.R,
                    G = args.Color.G,
                    B = args.Color.B,
                    A = args.Color.A
                },
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.tShockCancellationTokenSource.Cancel();
                this.tShockCancellationTokenSource.Dispose();

                TerrariaApi.Server.ServerApi.Hooks.ServerJoin.Deregister(this, HandlePlayerJoin);
                TerrariaApi.Server.ServerApi.Hooks.ServerLeave.Deregister(this, HandlePlayerLeave);
                TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= HandlePlayerPostLogin;
                TShockAPI.Hooks.PlayerHooks.PlayerLogout -= HandlePlayerLogout;

                TShockAPI.GetDataHandlers.PlayerSlot -= HandlePlayerInventorySlot;
                TShockAPI.GetDataHandlers.PlayerHP -= this.HandlePlayerHealth;
                TShockAPI.GetDataHandlers.PlayerMana -= this.HandlePlayerMana;
                TShockAPI.GetDataHandlers.PlayerBuff -= this.HandlePlayerBuff;
                TShockAPI.GetDataHandlers.PlayerBuffUpdate -= this.HandlePlayerBuffUpdate;

                TShockAPI.Hooks.PlayerHooks.PlayerChat -= this.HandlePlayerChat;
                TerrariaApi.Server.ServerApi.Hooks.ServerBroadcast.Deregister(this, this.HandleServerBroadcast);

                this.gRpcServer.KillAsync();
            }

            base.Dispose(disposing);
        }
    }
}
