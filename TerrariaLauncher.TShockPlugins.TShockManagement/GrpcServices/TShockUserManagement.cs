using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerrariaLauncher.Protos.CommonMessages;
using TerrariaLauncher.Protos.InstancePlugins.InstanceManagement;

namespace TerrariaLauncher.TShockPlugins.TShockManagement.GrpcServices
{
    class TShockUserManagement : TerrariaLauncher.Protos.InstancePlugins.InstanceManagement.InstanceUserManagement.InstanceUserManagementBase
    {
        public override Task<InstanceUser> CreateUser(CreateUserRequest request, ServerCallContext context)
        {
            var groupName = request.Group;
            if (String.IsNullOrWhiteSpace( groupName))
            {
                groupName = TShockAPI.Group.DefaultGroup.Name;
            }

            var user = new TShockAPI.DB.UserAccount()
            {
                Name = request.Name,
                Group = groupName
            };
            user.CreateBCryptHash(request.Password);
            try
            {
                TShockAPI.TShock.UserAccounts.AddUserAccount(user);
            }
            catch (TShockAPI.DB.GroupNotExistsException)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Group does not exists."));
            }
            catch (TShockAPI.DB.UserAccountExistsException)
            {
                throw new RpcException(new Status(StatusCode.AlreadyExists, "User name existed."));
            }
            catch (TShockAPI.DB.UserAccountManagerException)
            {
                throw new RpcException(new Status(StatusCode.Internal, "TShock internal exception."));
            }

            user = TShockAPI.TShock.UserAccounts.GetUserAccountByName(user.Name);

            return Task.FromResult(new InstanceUser()
            {
                Id = user.ID,
                Name = user.Name,
                Group = user.Group
            });
        }

        public override Task<VerifyUserPasswordResponse> VerifyUserPassword(VerifyUserPasswordRequest request, ServerCallContext context)
        {
            TShockAPI.DB.UserAccount user = null;
            switch (request.IdOrNameCase)
            {
                case VerifyUserPasswordRequest.IdOrNameOneofCase.Id:
                    user = TShockAPI.TShock.UserAccounts.GetUserAccountByID(request.Id);
                    break;
                case VerifyUserPasswordRequest.IdOrNameOneofCase.Name:
                    user = TShockAPI.TShock.UserAccounts.GetUserAccountByName(request.Name);
                    break;
                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Neither id nor name is provided."));
            }

            if (user is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User is not found."));
            }

            var isPasswordValid = user.VerifyPassword(request.Name);
            return Task.FromResult(new VerifyUserPasswordResponse()
            {
                IsPasswordValid = isPasswordValid
            });
        }

        public override Task<InstanceUser> GetUser(GetUserRequest request, ServerCallContext context)
        {
            TShockAPI.DB.UserAccount user = null;
            switch (request.IdOrNameCase)
            {
                case GetUserRequest.IdOrNameOneofCase.Id:
                    user = TShockAPI.TShock.UserAccounts.GetUserAccountByID(request.Id);
                    break;
                case GetUserRequest.IdOrNameOneofCase.Name:
                    user = TShockAPI.TShock.UserAccounts.GetUserAccountByName(request.Name);
                    break;
                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Neither id nor name is provided."));
            }

            if (user is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User is not found."));
            }

            return Task.FromResult(new InstanceUser()
            {
                Id = user.ID,
                Name = user.Name,
                Group = user.Group
            });
        }

        public override Task<InstanceUser> UpdateUser(UpdateUserRequest request, ServerCallContext context)
        {
            var user = TShockAPI.TShock.UserAccounts.GetUserAccountByID(request.Id);
            if (user is null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "User is not found."));
            }

            switch (request.UpdateFieldCase)
            {
                case UpdateUserRequest.UpdateFieldOneofCase.Name:
                    throw new RpcException(new Status(StatusCode.Unimplemented, "Changing user name is not support."));
                case UpdateUserRequest.UpdateFieldOneofCase.Group:
                    try
                    {
                        TShockAPI.TShock.UserAccounts.SetUserGroup(user, request.Group);
                    }
                    catch (TShockAPI.DB.GroupNotExistsException)
                    {
                        throw new RpcException(new Status(StatusCode.FailedPrecondition, "Group does not exist."));
                    }
                    break;
                case UpdateUserRequest.UpdateFieldOneofCase.Password:
                    TShockAPI.TShock.UserAccounts.SetUserAccountPassword(user, request.Password);
                    break;
                default:
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Update field "));
            }

            user = TShockAPI.TShock.UserAccounts.GetUserAccountByID(request.Id);
            return Task.FromResult(new InstanceUser()
            {
                Id = user.ID,
                Name = user.Name,
                Group = user.Group
            });
        }
    }
}
