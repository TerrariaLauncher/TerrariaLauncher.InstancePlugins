using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerrariaLauncher.Commons.DomainObjects;
using TerrariaLauncher.Protos.InstancePlugins.GameCoordinatorAgent;

namespace TerrariaLauncher.TShockPlugins.GameCoordinatorAgent.GrpcServices
{
    class InstanceService : TerrariaLauncher.Protos.InstancePlugins.GameCoordinatorAgent.InstanceService.InstanceServiceBase
    {
        Instance _instance;
        public InstanceService(IOptions<Instance> instanceOptions)
        {
            _instance = instanceOptions.Value;
        }

        public override Task<GetInstanceResponse> GetInstance(GetInstanceRequest request, ServerCallContext context)
        {
            var response = new GetInstanceResponse()
            {
                Id = this._instance.Id,
                Name = this._instance.Name,
                Realm = this._instance.Realm,
                Enabled = this._instance.Enabled,
                Host = this._instance.Host,
                Port = this._instance.Port,
                MaxSlots = this._instance.MaxSlots,
                Platform = this._instance.Platform,
                Version = this._instance.Version
            };
            return Task.FromResult(response);
        }
    }
}
