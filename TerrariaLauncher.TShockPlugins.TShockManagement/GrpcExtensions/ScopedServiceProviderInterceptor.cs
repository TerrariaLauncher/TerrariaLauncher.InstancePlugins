using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaLauncher.TShockPlugins.TShockManagement.GrpcExtensions
{
    public class ScopedServiceProviderInterceptor : Interceptor
    {
        private IServiceProvider serviceProvider;
        public ScopedServiceProviderInterceptor(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            using (var scope = this.serviceProvider.CreateScope())
            {
                try
                {
                    context.UserState.Add("ScopedServiceProvider", scope);
                    await continuation(request, responseStream, context).ConfigureAwait(false);
                }
                finally
                {
                    context.UserState.Remove("ScopedServiceProvider");
                }
            }
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            using (var scope = this.serviceProvider.CreateScope())
            {
                try
                {
                    context.UserState.Add("ScopedServiceProvider", scope);
                    return await continuation(request, context).ConfigureAwait(false);
                }
                finally
                {
                    context.UserState.Remove("ScopedServiceProvider");
                }
            }
        }
    }
}
