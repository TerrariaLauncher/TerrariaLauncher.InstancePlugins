using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using TerrariaLauncher.Commons.Consul;
using TerrariaLauncher.Commons.Consul.API.Agent.Services.Commands;
using TerrariaLauncher.Commons.Consul.API.Commons;
using TerrariaLauncher.Commons.Consul.API.DTOs;
using TerrariaLauncher.Commons.Consul.ConfigurationProvider;
using TerrariaLauncher.Commons.Consul.Extensions;
using TerrariaLauncher.Commons.DomainObjects;

namespace TerrariaLauncher.TShockPlugins.GameCoordinatorAgent
{
    [TerrariaApi.Server.ApiVersion(2, 1)]
    public class GameCoordinatorAgent : TerrariaApi.Server.TerrariaPlugin
    {
        public GameCoordinatorAgent(Main game) : base(game)
        {
            // TShock Plugin in has Order = 0.
            this.Order = 1;
            var harmony = new HarmonyLib.Harmony("TerrariaLauncher.TShockPlugins.GameCoordinatorAgent");
            harmony.PatchAll();
        }

        public override string Name => "Game Coordinator Agent";
        public override Version Version => new Version(1, 0, 0, 0);
        public override string Author => "Terraria Launcher";
        public override string Description => "Running an agent of TerrariaLauncher.Services.GameCoordinator.";
        public override string UpdateURL => "https://github.com/TerrariaLauncher";

        IHost host;
        IServiceScope serviceScope;
        IServiceProvider scopedServiceProvider;
        IConfiguration configuration;

        Grpc.Core.Server grpcServer;

        public override void Initialize()
        {
            TShockAPI.TShock tShockInstance = null;
            foreach (var pluginContainer in TerrariaApi.Server.ServerApi.Plugins)
            {
                if (pluginContainer.Plugin is TShockAPI.TShock)
                {
                    tShockInstance = pluginContainer.Plugin as TShockAPI.TShock;
                    break;
                }
            }

            if (tShockInstance is null) throw new InvalidOperationException("Fatal: TShock instance is not found in the plugin instances.");

            TerrariaApi.Server.ServerApi.Hooks.NetGreetPlayer.Register(this, this.HandleNetGreetPlayer);
            this.host = CreateHostBuilder(Array.Empty<string>()).Build();
            this.host.Start();
            this.serviceScope = this.host.Services.CreateScope();
            this.scopedServiceProvider = this.serviceScope.ServiceProvider;

            this.grpcServer = new Grpc.Core.Server()
            {
                Services =
                {
                    TerrariaLauncher.Protos.InstancePlugins.GameCoordinatorAgent.InstanceService.BindService(
                        this.scopedServiceProvider.GetRequiredService<
                            TerrariaLauncher.TShockPlugins.GameCoordinatorAgent.GrpcServices.InstanceService
                        >()
                    )
                },
                Ports = 
                {
                    new Grpc.Core.ServerPort("localhost", 0, Grpc.Core.ServerCredentials.Insecure)
                }
            };
            this.grpcServer.Start();

            this.configuration = this.scopedServiceProvider.GetRequiredService<IConfiguration>();
            var serviceRegister = configuration.GetSection("ConsulServiceRegister").Get<Registration>();

            var grpcPort = this.grpcServer.Ports.First();
            var serviceRegisterCommand = new RegisterServiceCommand()
            {
                ReplaceExistingChecks = true,
                Registration = serviceRegister
            };
            serviceRegisterCommand.Registration.Address = grpcPort.Host;
            serviceRegisterCommand.Registration.Port = grpcPort.BoundPort;
            serviceRegisterCommand.Registration.Check.TCP = $"{grpcPort.Host}:{grpcPort.BoundPort}";

            var consulCommandDispatcher = this.scopedServiceProvider.GetRequiredService<IConsulCommandDispatcher>();
            var dispatchTask = consulCommandDispatcher.Dispatch<RegisterServiceCommand, RegisterServiceCommandResult>(serviceRegisterCommand);
            SpinWait.SpinUntil(() => dispatchTask.IsCompleted);

            var instanceRpc = this.scopedServiceProvider.GetRequiredService<TerrariaLauncher.Protos.Services.GameCoordinator.Hub.Instances.InstancesClient>();
            instanceRpc.InstanceUp(new Protos.Services.GameCoordinator.Hub.InstanceUpRequest()
            {
                InstanceId = configuration["Instance:Id"],
            });
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureHostConfiguration(configurationBuilder => { })
            .ConfigureAppConfiguration((hostBuilderContext, configurationBuilder) =>
            {
                var tempConfigurationBuilder = new ConfigurationBuilder();
                var env = hostBuilderContext.HostingEnvironment;
                tempConfigurationBuilder.AddJsonFile("./ServerPlugins/TerrariaLauncher.TShockPlugins.GameCoordinatorAgent.json", optional: false, reloadOnChange: false);
                var tempConfigurationRoot = tempConfigurationBuilder.Build();

                configurationBuilder.AddJsonFile("./ServerPlugins/TerrariaLauncher.TShockPlugins.GameCoordinatorAgent.json", optional: false, reloadOnChange: false);
                var consulHost = tempConfigurationRoot.GetSection("Consul").Get<ConsulHostConfiguration>();
                var consulConfigurationKeys = tempConfigurationRoot.GetSection("ConsulConfigurationProvider:Keys").Get<string[]>();
                foreach (var key in consulConfigurationKeys)
                {
                    configurationBuilder.UseConsulConfiguration(consulHost, key);
                }
            })
            .ConfigureLogging((hostBuilderContext, loggingBuilder) =>
            {
                loggingBuilder.ClearProviders();
            })
            .ConfigureServices(ConfigureServices);

        static void ConfigureServices(HostBuilderContext hostBuilderContext, IServiceCollection services)
        {
            var consulHostConfiguration = hostBuilderContext.Configuration.GetSection("Consul").Get<ConsulHostConfiguration>();
            services.AddConsulService(consulHostConfiguration);

            services.AddSingleton<TerrariaLauncher.TShockPlugins.GameCoordinatorAgent.GrpcServices.InstanceService>();
            services.AddSingleton<TerrariaLauncher.TShockPlugins.GameCoordinatorAgent.GrpcServices.Users>();

            var hubUrl = hostBuilderContext.Configuration["Services:TerrariaLauncher.Services.GameCoordinator.Hub:Url"];
            /*
            services.AddGrpcClient<TerrariaLauncher.Protos.Services.GameCoordinator.Hub.Instances.InstancesClient>(options =>
            {
                options.Address = new Uri(hubUrl);
            });
            services.AddGrpcClient<TerrariaLauncher.Protos.Services.GameCoordinator.Hub.Users.UsersClient>(options =>
            {
                options.Address = new Uri(hubUrl);
            });
            */
            var hubGrpcChannel = new Grpc.Core.Channel(hubUrl, Grpc.Core.ChannelCredentials.Insecure);
            services.AddSingleton<TerrariaLauncher.Protos.Services.GameCoordinator.Hub.Users.UsersClient>((serviceProvider) => {
                return new Protos.Services.GameCoordinator.Hub.Users.UsersClient(hubGrpcChannel);
            });
            services.AddSingleton<TerrariaLauncher.Protos.Services.GameCoordinator.Hub.Instances.InstancesClient>((serviceProvider) => {
                return new Protos.Services.GameCoordinator.Hub.Instances.InstancesClient(hubGrpcChannel);
            });

            services.Configure<Instance>(hostBuilderContext.Configuration.GetSection("Instance"));
            services.PostConfigure<Instance>(instance =>
            {
                instance.Host = "localhost";
                instance.Port = TShockAPI.TShock.Config.Settings.ServerPort;
                instance.MaxSlots = TShockAPI.TShock.Config.Settings.MaxSlots;
                instance.Platform = "Desktop";
                instance.Version = Terraria.Main.assemblyVersionNumber;
            });
        }

        private void HandleNetGreetPlayer(TerrariaApi.Server.GreetPlayerEventArgs args)
        {
            var instanceId = this.configuration["InstanceId"];
            var player = TShockAPI.TShock.Players[args.Who];
            var usersRpc = this.scopedServiceProvider.GetRequiredService<TerrariaLauncher.Protos.Services.GameCoordinator.Hub.Users.UsersClient>();
            usersRpc.AssignUser(new Protos.Services.GameCoordinator.Hub.AssignUserRequest()
            {
                InstanceId = instanceId,
                PlayerName = player.Name
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var stoppingTask = this.host.StopAsync();
                SpinWait.SpinUntil(() => stoppingTask.IsCompleted);

                TerrariaApi.Server.ServerApi.Hooks.NetGreetPlayer.Deregister(this, this.HandleNetGreetPlayer);
            }

            base.Dispose(disposing);
        }
    }
}
