using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TerrariaLauncher.Protos.CommonMessages;
using TerrariaLauncher.Protos.InstancePlugins.InstanceManagement;
using TerrariaLauncher.TShockPlugins.TShockManagement.GrpcExtensions;

namespace TerrariaLauncher.TShockPlugins.TShockManagement.GrpcServices
{
    class TShockPlayerManagement : TerrariaLauncher.Protos.InstancePlugins.InstanceManagement.InstancePlayerManagement.InstancePlayerManagementBase
    {
        private TShockAPI.TSPlayer GetPlayerByNameOrId(dynamic nameOrId)
        {
            List<TShockAPI.TSPlayer> matchedPlayers = TShockAPI.TSPlayer.FindByNameOrID(nameOrId);
            if (matchedPlayers.Count < 1)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Player was not found."));
            }
            else if (matchedPlayers.Count > 1)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "More than one matched players. Please provide identical player name."));
            }

            return matchedPlayers[0];
        }

        public override Task<Player> GetPlayer(GetPlayerRequest request, ServerCallContext context)
        {
            TShockAPI.TSPlayer player = null;
            if (request.IdOrNameCase == GetPlayerRequest.IdOrNameOneofCase.Id)
            {
                player = GetPlayerByNameOrId(request.Id);
            }
            else if (request.IdOrNameCase == GetPlayerRequest.IdOrNameOneofCase.Name)
            {
                player = GetPlayerByNameOrId(request.Name);
            }
            else
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Id or name is not provided."));
            }

            return Task.FromResult(new Player()
            {
                Id = player.Index,
                Name = player.Name,
                User = new Player.Types.User()
                {
                    Id = player.Account?.ID ?? -1,
                    Name = player.Account?.Name ?? ""
                },
                Group = new Player.Types.Group()
                {
                    Name = player.Group?.Name ?? ""
                }
            });
        }

        public override Task<GetPlayersResponse> GetPlayers(GetPlayersRequest request, ServerCallContext context)
        {
            var payload = new GetPlayersResponse()
            {
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };
            foreach (var player in TShockAPI.TShock.Players)
            {
                if (player != null && player.Active)
                {
                    payload.Players.Add(new Player()
                    {
                        Id = player.Index,
                        Name = player.Name,
                        User = new Player.Types.User()
                        {
                            Id = player.Account?.ID ?? -1,
                            Name = player.Account?.Name ?? ""
                        },
                        Group = new Player.Types.Group()
                        {
                            Name = player.Group?.Name ?? ""
                        }
                    });
                }
            }
            return Task.FromResult(payload);
        }

        private PlayerData.Types.Inventory.Types.Item Create_PlayerData_Inventory_Item(Terraria.Item item)
        {
            if (item != null && item.active)
            {
                return new PlayerData.Types.Inventory.Types.Item()
                {
                    Id = item.type,
                    Prefix = item.prefix,
                    Stack = item.stack
                };
            }
            else
            {
                return new PlayerData.Types.Inventory.Types.Item()
                {
                    Id = 0,
                    Prefix = 0,
                    Stack = 0
                };
            }
        }

        private PlayerData.Types.Inventory Create_PlayerData_Inventory(TShockAPI.TSPlayer player)
        {
            var inventory = new PlayerData.Types.Inventory();

            var TerrariaHotBarIndex = Commons.DomainObjects.Inventory.TerrariaHotBarIndex;
            for (int i = TerrariaHotBarIndex.Start; i < TerrariaHotBarIndex.ExclusiveEnd; ++i)
            {
                inventory.HotBar.Add(Create_PlayerData_Inventory_Item(player.TPlayer.inventory[i]));
            }

            var TerrariaMainInventoryIndex = Commons.DomainObjects.Inventory.TerrariaMainInventoryIndex;
            for (int i = TerrariaMainInventoryIndex.Start; i < TerrariaMainInventoryIndex.ExclusiveEnd; ++i)
            {
                inventory.MainInventory.Add(Create_PlayerData_Inventory_Item(player.TPlayer.inventory[i]));
            }

            var TerrariaCoinIndex = Commons.DomainObjects.Inventory.TerrariaCoinIndex;
            for (int i = TerrariaCoinIndex.Start; i < TerrariaCoinIndex.ExclusiveEnd; ++i)
            {
                inventory.Coins.Add(Create_PlayerData_Inventory_Item(player.TPlayer.inventory[i]));
            }

            var TerrariaAmmoIndex = Commons.DomainObjects.Inventory.TerrariaAmmoIndex;
            for (int i = TerrariaAmmoIndex.Start; i < TerrariaAmmoIndex.ExclusiveEnd; ++i)
            {
                inventory.Ammo.Add(Create_PlayerData_Inventory_Item(player.TPlayer.inventory[i]));
            }

            var TerrariaCursorIndex = Commons.DomainObjects.Inventory.TerrariaCursorIndex;
            for (int i = TerrariaCursorIndex.Start; i < TerrariaCursorIndex.ExclusiveEnd; ++i)
            {
                inventory.Cursor.Add(Create_PlayerData_Inventory_Item(player.TPlayer.inventory[i]));
            }

            var TerrariaArmorIndex = Commons.DomainObjects.Inventory.TerrariaArmorIndex;
            for (int i = TerrariaArmorIndex.Start; i < TerrariaArmorIndex.ExclusiveEnd; ++i)
            {
                inventory.Armor.Add(Create_PlayerData_Inventory_Item(player.TPlayer.armor[i]));
            }

            var TerrariaAccessoryIndex = Commons.DomainObjects.Inventory.TerrariaAccessoryIndex;
            for (int i = TerrariaAccessoryIndex.Start; i < TerrariaAccessoryIndex.ExclusiveEnd; ++i)
            {
                inventory.Accessories.Add(Create_PlayerData_Inventory_Item(player.TPlayer.armor[i]));
            }

            var TerrariaVanityArmorIndex = Commons.DomainObjects.Inventory.TerrariaVanityArmorIndex;
            for (int i = TerrariaVanityArmorIndex.Start; i < TerrariaVanityArmorIndex.ExclusiveEnd; ++i)
            {
                inventory.VanityArmor.Add(Create_PlayerData_Inventory_Item(player.TPlayer.armor[i]));
            }

            var TerrariaVanityAccessoryIndex = Commons.DomainObjects.Inventory.TerrariaVanityAccessoryIndex;
            for (int i = TerrariaVanityAccessoryIndex.Start; i < TerrariaVanityAccessoryIndex.ExclusiveEnd; ++i)
            {
                inventory.VanityAccessories.Add(Create_PlayerData_Inventory_Item(player.TPlayer.armor[i]));
            }

            var TerrariaArmorDyeIndex = Commons.DomainObjects.Inventory.TerrariaArmorDyeIndex;
            for (int i = TerrariaArmorDyeIndex.Start; i < TerrariaArmorDyeIndex.ExclusiveEnd; ++i)
            {
                inventory.ArmorDye.Add(Create_PlayerData_Inventory_Item(player.TPlayer.dye[i]));
            }

            var TerrariaAccessoryDyeIndex = Commons.DomainObjects.Inventory.TerrariaAccessoryDyeIndex;
            for (int i = TerrariaAccessoryDyeIndex.Start; i < TerrariaAccessoryDyeIndex.ExclusiveEnd; ++i)
            {
                inventory.AccessoryDye.Add(Create_PlayerData_Inventory_Item(player.TPlayer.dye[i]));
            }

            var TerrariaEquipmentIndex = Commons.DomainObjects.Inventory.TerrariaEquipmentIndex;
            for (int i = TerrariaEquipmentIndex.Start; i < TerrariaEquipmentIndex.ExclusiveEnd; ++i)
            {
                inventory.Equipment.Add(Create_PlayerData_Inventory_Item(player.TPlayer.miscEquips[i]));
            }

            var TerrariaEquipmentDyeIndex = Commons.DomainObjects.Inventory.TerrariaEquipmentDyeIndex;
            for (int i = TerrariaEquipmentDyeIndex.Start; i < TerrariaEquipmentDyeIndex.ExclusiveEnd; ++i)
            {
                inventory.EquipmentDye.Add(Create_PlayerData_Inventory_Item(player.TPlayer.miscDyes[i]));
            }

            var TerrariaPiggyBankIndex = Commons.DomainObjects.Inventory.TerrariaPiggyBankIndex;
            for (int i = TerrariaPiggyBankIndex.Start; i < TerrariaPiggyBankIndex.ExclusiveEnd; ++i)
            {
                inventory.PiggyBank.Add(Create_PlayerData_Inventory_Item(player.TPlayer.bank.item[i]));
            }

            var TerrariaSafeIndex = Commons.DomainObjects.Inventory.TerrariaSafeIndex;
            for (int i = TerrariaSafeIndex.Start; i < TerrariaSafeIndex.ExclusiveEnd; ++i)
            {
                inventory.Safe.Add(Create_PlayerData_Inventory_Item(player.TPlayer.bank2.item[i]));
            }

            inventory.Trash.Add(Create_PlayerData_Inventory_Item(player.TPlayer.trashItem));

            var TerrariaDefenderForgeIndex = Commons.DomainObjects.Inventory.TerrariaDefenderForgeIndex;
            for (int i = TerrariaDefenderForgeIndex.Start; i < TerrariaDefenderForgeIndex.ExclusiveEnd; ++i)
            {
                inventory.DefenderForge.Add(Create_PlayerData_Inventory_Item(player.TPlayer.bank3.item[i]));
            }

            var TerrariaVoidVaultIndex = Commons.DomainObjects.Inventory.TerrariaVoidVaultIndex;
            for (int i = TerrariaVoidVaultIndex.Start; i < TerrariaVoidVaultIndex.ExclusiveEnd; ++i)
            {
                inventory.VoidVault.Add(Create_PlayerData_Inventory_Item(player.TPlayer.bank4.item[i]));
            }

            return inventory;
        }

        private PlayerData Create_PlayerData(TShockAPI.TSPlayer player)
        {
            return new PlayerData()
            {
                Player = new PlayerData.Types.Player()
                {
                    Id = player.Index,
                    Name = player.Name,
                },
                User = new PlayerData.Types.User()
                {
                    Id = player?.Account.ID ?? -1,
                    Name = player?.Account.Name ?? ""
                },
                Group = new PlayerData.Types.Group()
                {
                    Name = player?.Group.Name
                },
                Inventory = Create_PlayerData_Inventory(player),
                Health = new PlayerData.Types.Health()
                {
                    Current = player.TPlayer.statLife,
                    Base = player.TPlayer.statLifeMax,
                    Max = player.TPlayer.statLifeMax2
                },
                Mana = new PlayerData.Types.Mana()
                {
                    Current = player.TPlayer.statMana,
                    Base = player.TPlayer.statManaMax,
                    Max = player.TPlayer.statManaMax2
                },
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };
        }

        public override Task<PlayerData> GetPlayerData(GetPlayerDataRequest request, ServerCallContext context)
        {
            TShockAPI.TSPlayer player;
            switch (request.IdOrNameCase)
            {
                case GetPlayerDataRequest.IdOrNameOneofCase.Id:
                    player = GetPlayerByNameOrId(request.Id);
                    break;
                case GetPlayerDataRequest.IdOrNameOneofCase.Name:
                    player = GetPlayerByNameOrId(request.Name);
                    break;
                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Id or name is not provided."));
            }

            var payload = Create_PlayerData(player);
            return Task.FromResult(payload);
        }

        public override Task<GetPlayersDataResponse> GetPlayersData(GetPlayersDataRequest request, ServerCallContext context)
        {
            var payload = new GetPlayersDataResponse()
            {
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };

            foreach (var player in TShockAPI.TShock.Players)
            {
                if (player != null && player.Active)
                {
                    payload.Data.Add(Create_PlayerData(player));
                }
            }

            return Task.FromResult(payload);
        }

        public override Task<KickAllPlayersResponse> KickAllPlayers(KickAllPlayersRequest request, ServerCallContext context)
        {
            var payload = new KickAllPlayersResponse();
            foreach (var player in TShockAPI.TShock.Players)
            {
                if (player != null && player.Active)
                {
                    if (player.Kick(reason: request.Reason, force: request.Force, silent: request.Silent, adminUserName: request.WhoOrdered, saveSSI: true))
                    {
                        payload.KickedPlayers.Add(new KickAllPlayersResponse.Types.Player()
                        {
                            Id = player.Index,
                            Name = player.Name
                        });
                    }
                }
            }
            TShockAPI.TShock.Utils.Broadcast($"All players was kicked! The reason was \"{request.Reason}\".", new Microsoft.Xna.Framework.Color(255, 0, 0));

            return Task.FromResult(payload);
        }

        public override Task<KickPlayerResponse> KickPlayer(KickPlayerRequest request, ServerCallContext context)
        {
            TShockAPI.TSPlayer player;
            switch (request.IdOrNameCase)
            {
                case KickPlayerRequest.IdOrNameOneofCase.Id:
                    player = GetPlayerByNameOrId(request.Id);
                    break;
                case KickPlayerRequest.IdOrNameOneofCase.Name:
                    player = GetPlayerByNameOrId(request.Name);
                    break;
                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Id or name is not provided."));
            }

            var kickResult = player.Kick(
                reason: request.Reason,
                force: request.Force,
                silent: request.Silient,
                adminUserName: request.WhoOrdered,
                saveSSI: true
            );
            return Task.FromResult(new KickPlayerResponse()
            {
                Kicked = kickResult
            });
        }

        public override Task<KillAllPlayersResponse> KillAllPlayers(KillAllPlayersRequest request, ServerCallContext context)
        {
            var payload = new KillAllPlayersResponse();

            foreach (var player in TShockAPI.TShock.Players)
            {
                if (player != null && player.Active)
                {
                    player.KillPlayer();
                    payload.KilledPlayers.Add(new KillAllPlayersResponse.Types.Player()
                    {
                        Id = player.Index,
                        Name = player.Name
                    });

                    if (!request.Silent)
                    {
                        player.SendWarningMessage($"\"{request.WhoOrdered}\" just terminated your in-game character!");
                        player.SendWarningMessage($"The reason was \"{request.Reason}\".");
                    }
                }
            }

            return Task.FromResult(payload);
        }

        public override Task<KillPlayerReponse> KillPlayer(KillPlayerRequest request, ServerCallContext context)
        {
            TShockAPI.TSPlayer player;
            switch (request.IdOrNameCase)
            {
                case KillPlayerRequest.IdOrNameOneofCase.Id:
                    player = GetPlayerByNameOrId(request.Id);
                    break;
                case KillPlayerRequest.IdOrNameOneofCase.Name:
                    player = GetPlayerByNameOrId(request.Name);
                    break;
                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Id or name is not provided."));
            }

            player.KillPlayer();
            if (!request.Silent)
            {
                player.SendWarningMessage($"\"{request.WhoOrdered}\" just terminated your in-game character!");
                player.SendWarningMessage($"The reason was \"{request.Reason}\".");
            }

            return Task.FromResult(new KillPlayerReponse()
            {
                Killed = true
            });
        }

        private async Task WriteServerStreamWithDataFromBroadcaster<TResponse>(IServerStreamWriter<TResponse> responseStream, ServerCallContext context, Func<IEnumerable<TResponse>> initializeResponses = null)
        {
            var serviceProvider = context.GetScopedServiceProvider();
            var broadcaster = serviceProvider.GetRequiredService<BroadcastBlock<TResponse>>();

            var bufferBlock = new BufferBlock<TResponse>(new DataflowBlockOptions()
            {
                CancellationToken = context.CancellationToken
            });
            var linkToBufferBlock = broadcaster.LinkTo(bufferBlock);
            // Dirty: Discard the last item stucks in broadcast block that transfered into buffer block.
            if (broadcaster.TryReceive(out _))
                _ = bufferBlock.Receive();

            try
            {
                if (initializeResponses != null)
                {
                    var initialResponses = initializeResponses();
                    foreach (var initialResponse in initialResponses)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        await responseStream.WriteAsync(initialResponse).ConfigureAwait(false);
                    }
                }

                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var payload = await bufferBlock.ReceiveAsync().ConfigureAwait(false);
                    await responseStream.WriteAsync(payload).ConfigureAwait(false);
                }
            }
            finally
            {
                bufferBlock.Complete();
                linkToBufferBlock.Dispose();
            }
        }

        public override async Task TrackPlayerSession(TrackPlayerSessionRequest request, IServerStreamWriter<TrackPlayerSessionResponse> responseStream, ServerCallContext context)
        {
            IList<TrackPlayerSessionResponse> CreateInitialResponses()
            {
                var responses = new List<TrackPlayerSessionResponse>();
                foreach (var player in TShockAPI.TShock.Players)
                {
                    if (player != null && player.Active)
                    {
                        responses.Add(new TrackPlayerSessionResponse()
                        {
                            EventType = TrackPlayerSessionResponse.Types.EventType.Initial,
                            Player = new TrackPlayerSessionResponse.Types.Player()
                            {
                                Id = player.Index,
                                Name = player.Name
                            },
                            User = new TrackPlayerSessionResponse.Types.User()
                            {
                                Id = player.Account?.ID ?? -1,
                                Name = player.Account?.Name ?? ""
                            },
                            Group = new TrackPlayerSessionResponse.Types.Group()
                            {
                                Name = player.Group?.Name ?? ""
                            },
                            Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
                        });
                    }
                }
                return responses;
            }
            Func<IEnumerable<TrackPlayerSessionResponse>> initialFunc = null;
            if (request.NeedInitialization)
            {
                initialFunc = CreateInitialResponses;
            }
            await WriteServerStreamWithDataFromBroadcaster(responseStream, context, initialFunc).ConfigureAwait(false);
        }

        public override async Task TrackPlayerData(TrackPlayerDataRequest request, IServerStreamWriter<TrackPlayerDataResponse> responseStream, ServerCallContext context)
        {
            IList<TrackPlayerDataResponse> CreateInitialSlotResponses()
            {
                TrackPlayerDataResponse CreateInitialSlotResponse(TShockAPI.TSPlayer player, int slot, Terraria.Item item)
                {
                    return new TrackPlayerDataResponse()
                    {
                        Player = new TrackPlayerDataResponse.Types.Player
                        {
                            Id = player.Index,
                            Name = player.Name
                        },
                        Details = Google.Protobuf.WellKnownTypes.Any.Pack(
                            new TrackPlayerDataResponse.Types.SlotEventDetails()
                            {
                                Slot = slot,
                                Item = item.type,
                                Prefix = item.prefix,
                                Stack = item.stack
                            }),
                        Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                        IsInitial = true
                    };
                }

                var initialReponses = new List<TrackPlayerDataResponse>();
                for (int playerIndex = 0; playerIndex < TShockAPI.TShock.Players.Length; ++playerIndex)
                {
                    var player = TShockAPI.TShock.Players[playerIndex];
                    if (player == null || !player.Active)
                    {
                        for (int i = 0; i < Commons.DomainObjects.Inventory.TotalSlots; ++i)
                        {
                            initialReponses.Add(new TrackPlayerDataResponse()
                            {
                                Player = new TrackPlayerDataResponse.Types.Player()
                                {
                                    Id = playerIndex,
                                    Name = ""
                                },
                                Details = Google.Protobuf.WellKnownTypes.Any.Pack(
                                    new TrackPlayerDataResponse.Types.SlotEventDetails()
                                    {
                                        Slot = i,
                                        Item = 0,
                                        Prefix = 0,
                                        Stack = 0
                                    }),
                                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                                IsInitial = true
                            });
                        }
                    }
                    else
                    {
                        int slot = 0;
                        var TerrariaHotBarIndex = Commons.DomainObjects.Inventory.TerrariaHotBarIndex;
                        for (int i = TerrariaHotBarIndex.Start; i < TerrariaHotBarIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.inventory[i]));
                        }

                        var TerrariaMainInventoryIndex = Commons.DomainObjects.Inventory.TerrariaMainInventoryIndex;
                        for (int i = TerrariaMainInventoryIndex.Start; i < TerrariaMainInventoryIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.inventory[i]));
                        }

                        var TerrariaCoinIndex = Commons.DomainObjects.Inventory.TerrariaCoinIndex;
                        for (int i = TerrariaCoinIndex.Start; i < TerrariaCoinIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.inventory[i]));
                        }

                        var TerrariaAmmoIndex = Commons.DomainObjects.Inventory.TerrariaAmmoIndex;
                        for (int i = TerrariaAmmoIndex.Start; i < TerrariaAmmoIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.inventory[i]));
                        }

                        var TerrariaCursorIndex = Commons.DomainObjects.Inventory.TerrariaCursorIndex;
                        for (int i = TerrariaCursorIndex.Start; i < TerrariaCursorIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.inventory[i]));
                        }

                        var TerrariaArmorIndex = Commons.DomainObjects.Inventory.TerrariaArmorIndex;
                        for (int i = TerrariaArmorIndex.Start; i < TerrariaArmorIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.armor[i]));
                        }

                        var TerrariaAccessoryIndex = Commons.DomainObjects.Inventory.TerrariaAccessoryIndex;
                        for (int i = TerrariaAccessoryIndex.Start; i < TerrariaAccessoryIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.armor[i]));
                        }

                        var TerrariaVanityArmorIndex = Commons.DomainObjects.Inventory.TerrariaVanityArmorIndex;
                        for (int i = TerrariaVanityArmorIndex.Start; i < TerrariaVanityArmorIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.armor[i]));
                        }

                        var TerrariaVanityAccessoryIndex = Commons.DomainObjects.Inventory.TerrariaVanityAccessoryIndex;
                        for (int i = TerrariaVanityAccessoryIndex.Start; i < TerrariaVanityAccessoryIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.armor[i]));
                        }

                        var TerrariaArmorDyeIndex = Commons.DomainObjects.Inventory.TerrariaArmorDyeIndex;
                        for (int i = TerrariaArmorDyeIndex.Start; i < TerrariaArmorDyeIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.dye[i]));
                        }

                        var TerrariaAccessoryDyeIndex = Commons.DomainObjects.Inventory.TerrariaAccessoryDyeIndex;
                        for (int i = TerrariaAccessoryDyeIndex.Start; i < TerrariaAccessoryDyeIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.dye[i]));
                        }

                        var TerrariaEquipmentIndex = Commons.DomainObjects.Inventory.TerrariaEquipmentIndex;
                        for (int i = TerrariaEquipmentIndex.Start; i < TerrariaEquipmentIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.miscEquips[i]));
                        }

                        var TerrariaEquipmentDyeIndex = Commons.DomainObjects.Inventory.TerrariaEquipmentDyeIndex;
                        for (int i = TerrariaEquipmentDyeIndex.Start; i < TerrariaEquipmentDyeIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.miscDyes[i]));
                        }

                        var TerrariaPiggyBankIndex = Commons.DomainObjects.Inventory.TerrariaPiggyBankIndex;
                        for (int i = TerrariaPiggyBankIndex.Start; i < TerrariaPiggyBankIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.bank.item[i]));
                        }

                        var TerrariaSafeIndex = Commons.DomainObjects.Inventory.TerrariaSafeIndex;
                        for (int i = TerrariaSafeIndex.Start; i < TerrariaSafeIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.bank2.item[i]));
                        }

                        initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.trashItem));

                        var TerrariaDefenderForgeIndex = Commons.DomainObjects.Inventory.TerrariaDefenderForgeIndex;
                        for (int i = TerrariaDefenderForgeIndex.Start; i < TerrariaDefenderForgeIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.bank3.item[i]));
                        }

                        var TerrariaVoidVaultIndex = Commons.DomainObjects.Inventory.TerrariaVoidVaultIndex;
                        for (int i = TerrariaVoidVaultIndex.Start; i < TerrariaVoidVaultIndex.ExclusiveEnd; ++i)
                        {
                            initialReponses.Add(CreateInitialSlotResponse(player, slot++, player.TPlayer.bank4.item[i]));
                        }
                    }
                }
                return initialReponses;
            }
            IList<TrackPlayerDataResponse> CreatInitialHealthResponses()
            {
                var initalResponses = new List<TrackPlayerDataResponse>();
                foreach (var player in Terraria.Main.player)
                {
                    initalResponses.Add(new TrackPlayerDataResponse()
                    {
                        Player = new TrackPlayerDataResponse.Types.Player()
                        {
                            Id = player.whoAmI
                        },
                        Details = Google.Protobuf.WellKnownTypes.Any.Pack(
                            new TrackPlayerDataResponse.Types.HealthEventDetails()
                            {
                                Current = player.statLife,
                                Base = player.statLifeMax,
                                Max = player.statLifeMax2
                            }),
                        IsInitial = true,
                        Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
                    });
                }
                return initalResponses;
            }
            IList<TrackPlayerDataResponse> CreatInitialManaResponses()
            {
                var initalResponses = new List<TrackPlayerDataResponse>();
                foreach (var player in Terraria.Main.player)
                {
                    initalResponses.Add(new TrackPlayerDataResponse()
                    {
                        Player = new TrackPlayerDataResponse.Types.Player()
                        {
                            Id = player.whoAmI
                        },
                        Details = Google.Protobuf.WellKnownTypes.Any.Pack(
                            new TrackPlayerDataResponse.Types.ManaEventDetails()
                            {
                                Current = player.statMana,
                                Base = player.statManaMax,
                                Max = player.statManaMax2
                            }),
                        IsInitial = true,
                        Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
                    });
                }
                return initalResponses;
            }
            IList<TrackPlayerDataResponse> CreateInitialResponses()
            {
                var initalResponses = new List<TrackPlayerDataResponse>();
                initalResponses.AddRange(CreateInitialSlotResponses());
                initalResponses.AddRange(CreatInitialHealthResponses());
                initalResponses.AddRange(CreatInitialManaResponses());
                return initalResponses;
            }

            Func<IEnumerable<TrackPlayerDataResponse>> initalFunc = null;
            if (request.NeedInitialization)
            {
                initalFunc = CreateInitialResponses;
            }

            await WriteServerStreamWithDataFromBroadcaster(responseStream, context, initalFunc);
        }

        public override async Task TrackPlayerChat(TrackPlayerChatRequest request, IServerStreamWriter<PlayerChatDetails> responseStream, ServerCallContext context)
        {
            await WriteServerStreamWithDataFromBroadcaster(responseStream, context)
                .ConfigureAwait(false);
        }

        public override async Task TrackServerBroadcast(TrackServerBroadcastRequest request, IServerStreamWriter<ServerBroadcastDetails> responseStream, ServerCallContext context)
        {
            await WriteServerStreamWithDataFromBroadcaster(responseStream, context)
                .ConfigureAwait(false);
        }
    }
}
