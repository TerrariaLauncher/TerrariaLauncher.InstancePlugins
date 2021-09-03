using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerrariaLauncher.Protos.InstancePlugins.GameCoordinatorAgent;

namespace TerrariaLauncher.TShockPlugins.GameCoordinatorAgent.GrpcServices
{
    class Users : TerrariaLauncher.Protos.InstancePlugins.GameCoordinatorAgent.Users.UsersBase
    {
        public override Task<AssignUserResponse> AssignUser(AssignUserRequest request, ServerCallContext context)
        {
            var targetPlayer = TShockAPI.TShock.Players.FirstOrDefault(player => player.Name == request.PlayerName);
            if (targetPlayer is null || !targetPlayer.Active)
                throw new RpcException(new Status(StatusCode.NotFound, "Player name did not exist on this instance."));

            TShockAPI.TShock.Players[targetPlayer.Index].Account =
                new TShockAPI.DB.UserAccount(
                    request.Name,
                    request.Password,
                    request.UUID,
                    request.Group,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            TShockAPI.TShock.Log.ConsoleInfo($"{request.PlayerName} logged in as {request.Name} successfully.");

            return Task.FromResult(new AssignUserResponse() { });
        }
    }
}
