using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ConsoleApp.ServiceBus;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Unicast;
using Serilog;
using Serilog.Events;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace ConsoleApp
{
    public static class Program
    {
        private static bool _commandHandlerRegistered;

        public static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
            try
            {
                await CreateHost(args).RunAsync();
            }
            catch (Exception exception)
            {
                Log.Fatal(exception, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }

            return 0;
        }

        private static IHost CreateHost(string[] args)
        {
            var host = CreateDefaultBuilder(args)
                .UseSerilog()
                .UseNServiceBus(hostContext =>
                {
                    var endpoint = EndpointFactory.CreateEndpoint("MyEndpoint");
                    // configure endpoint here
                    var transport = endpoint.UseTransport<LearningTransport>();
                    transport.Routing().RouteToEndpoint(Assembly.GetExecutingAssembly(), "MyEndpoint");
                    endpoint.HandleCommand<SomeCommand>(hostContext);
                    var pipe = endpoint.Pipeline;
                    pipe.Register(
                        typeof(AsyncScopeProviderBehavior),
                        "Begins an async scope to be used by the DI container to resolve instances in an incoming message pipeline.");

                    return endpoint;
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddSimpleInjector(ctx.GetContainer(), options =>
                    {
                        // Hooks hosted services into the Generic Host pipeline
                        // while resolving them through Simple Injector
                        options.AddHostedService<Worker>();

                        // Allows injection of ILogger
                        // application components.
                        options.AddLogging();
                        var container = options.Container;
                        container.AddMediatr(Assembly.GetExecutingAssembly());
                        container.Register<WorkerDependency>(Lifestyle.Singleton);
                    });
                })
                .UseConsoleLifetime()
                .Build();

            host.UseSimpleInjector(host.GetContainer());

            // Register application components.
            // probably not doable before asp net has started, and will be called upon first resolution
            //container.Verify();

            return host;
        }

        private static IHostBuilder CreateDefaultBuilder(string[] args)
        {
            return new HostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureHostConfiguration(hostConfig =>
                {
                    hostConfig.AddEnvironmentVariables("DOTNET_");
                    if (args != null) hostConfig.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((builderContext, appConfig) =>
                {
                    var env = builderContext.HostingEnvironment;
                    //var country = builderContext.Configuration.GetSection("country");
                    appConfig
                        .AddJsonFile("appSettings.json", true, true)
                        .AddJsonFile($"appSettings.{env.EnvironmentName}.json", true, true)
                        // TODO: load country config files here
                        ;

                    appConfig.AddEnvironmentVariables();

                    if (args != null) appConfig.AddCommandLine(args);
                })
                .UseDefaultServiceProvider((context, options) =>
                {
                    options.ValidateScopes = true;
                    options.ValidateOnBuild = true;
                });
        }

        private static void HandleCommand<TCommand>(this EndpointConfiguration endpointConfiguration,
            HostBuilderContext hostBuilderContext)
            where TCommand : class, IRequest
        {
            var s = endpointConfiguration.GetSettings();
            var mhr = s.GetOrCreate<MessageHandlerRegistry>();
            mhr.RegisterHandler(typeof(MediatedRequestMessageHandler<TCommand>));
            var container = hostBuilderContext.GetContainer();
            if (!_commandHandlerRegistered)
            {
                container.Register(typeof(MediatedRequestMessageHandler<>),
                    typeof(MediatedRequestMessageHandler<>),
                    Lifestyle.Singleton);
                _commandHandlerRegistered = true;
            }

            endpointConfiguration.RegisterComponents(components =>
            {
                components.ConfigureComponent(container.GetInstance<MediatedRequestMessageHandler<TCommand>>,
                    DependencyLifecycle.InstancePerUnitOfWork);
            });
        }
    }

    public static class HostBuilderExtensions
    {
        private static Container CreateContainer()
        {
            var container = new Container();
            container.Options.DefaultLifestyle = Lifestyle.Scoped;
            container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            return container;
        }

        public static Container GetContainer(this HostBuilderContext context)
        {
            Container container;
            var containerPresent = context.Properties.TryGetValue("container", out var containerAsObject);
            if (!containerPresent)
            {
                container = CreateContainer();
                context.Properties.Add("container", container);
            }
            else
            {
                container = (Container) containerAsObject;
            }

            return container;
        }

        public static Container GetContainer(this IHost host)
        {
            return host.Services.GetRequiredService<Container>();
        }
    }
}