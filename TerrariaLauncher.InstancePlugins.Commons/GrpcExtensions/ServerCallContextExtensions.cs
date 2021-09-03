using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace TerrariaLauncher.InstancePlugins.Commons.GrpcExtensions
{
    public static class ServerCallContextExtensions
    {
        public static IServiceProvider GetScopedServiceProvider(this ServerCallContext context)
        {
            if (!context.UserState.TryGetValue("ScopedServiceProvider", out var found))
            {
                return null;
            }
            else
            {
                return found as IServiceProvider;
            }
        }
    }
}
