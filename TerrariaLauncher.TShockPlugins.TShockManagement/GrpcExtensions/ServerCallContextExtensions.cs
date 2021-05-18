﻿using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaLauncher.TShockPlugins.TShockManagement.GrpcExtensions
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
