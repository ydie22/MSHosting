using System;
using System.Reflection;
using System.Threading.Tasks;
using ConsoleApp.ServiceBus;
using MediatR;
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
            //var container = new Container();
            //container.Options.DefaultLifestyle = Lifestyle.Scoped;
            //container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            var hostBuilder = new HostBuilder()
                .ConfigureHostConfiguration(configHost => { })
                .ConfigureAppConfiguration((hostContext, configApp) => { })
                .UseSerilog()
                .UseNServiceBus(hostContext =>
                {
                    var endpoint = new EndpointConfiguration("MyEndpoint");
                    // configure endpoint here
                    var transport = endpoint.UseTransport<LearningTransport>();
                    transport.Routing().RouteToEndpoint(Assembly.GetExecutingAssembly(), "MyEndpoint");
                    endpoint.HandleCommand<SomeCommand>();
                    var pipe = endpoint.Pipeline;
                    pipe.Register(
                        typeof(AsyncScopeProviderBehavior),
                        "Begins an async scope to be used by the DI container to resolve instances in an incoming message pipeline.");

                    return endpoint;
                })
                .ConfigureSimpleInjector((hostContext, services, container) =>
                {
                    // done when adding bus and calling HandleCommand
                    services.AddScoped(typeof(MediatedRequestMessageHandler<SomeCommand>),
                        _ => container.GetInstance<MediatedRequestMessageHandler<SomeCommand>>());
                    container.Register(typeof(MediatedRequestMessageHandler<>), typeof(MediatedRequestMessageHandler<>),
                        Lifestyle.Singleton);
                    //container.AddRegistrations<StuffRegistration>(hostContext);
                    container.AddMediatr(Assembly.GetExecutingAssembly());
                    container.Register<WorkerDependency>(Lifestyle.Singleton);
                })
                .UseConsoleLifetime();
            var host = hostBuilder
                .Build()
                .UseSimpleInjector(hostBuilder.GetContainer());

            // Register application components.
            // probably not doable before asp net has started, and will be called upon first resolution
            //container.Verify();

            return host;
        }

        private static void HandleCommand<TCommand>(this EndpointConfiguration endpointConfiguration)
            where TCommand : class, IRequest
        {
            var s = endpointConfiguration.GetSettings();
            var mhr = s.GetOrCreate<MessageHandlerRegistry>();
            mhr.RegisterHandler(typeof(MediatedRequestMessageHandler<TCommand>));
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

        public static IHostBuilder ConfigureSimpleInjector(this IHostBuilder builder,
            Action<HostBuilderContext, IServiceCollection, Container> configurationDelegate)
        {
            var container = GetContainer(builder);

            builder.ConfigureServices((ctx, services) => { configurationDelegate(ctx, services, container); });
            return builder;
        }

        public static Container GetContainer(this IHostBuilder builder)
        {
            Container container;
            var containerPresent = builder.Properties.TryGetValue("container", out var containerAsObject);
            if (!containerPresent)
            {
                container = CreateContainer();
                builder.Properties.Add("container", container);
                builder.ConfigureServices((ctx, services) =>
                {
                    services.AddSimpleInjector(container, options =>
                    {
                        // Hooks hosted services into the Generic Host pipeline
                        // while resolving them through Simple Injector
                        options.AddHostedService<Worker>();

                        // Allows injection of ILogger & IStringLocalizer dependencies into
                        // application components.
                        options.AddLogging();
                    });
                });
            }
            else
            {
                container = (Container) containerAsObject;
            }

            return container;
        }
    }
}