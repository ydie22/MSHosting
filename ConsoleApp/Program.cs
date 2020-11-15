using System;
using System.Reflection;
using System.Threading.Tasks;
using ConsoleApp.Grpc;
using ConsoleApp.ServiceBus;
using Grpc.AspNetCore.Server;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Server;
using Serilog;
using SimpleInjector;

namespace ConsoleApp
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                //.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss} {SourceContext} [{Level}] {Message}{NewLine}{Exception}")
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
            var host = HostBuilderHelper.CreateBuilder<Startup>(args)
                .UseSerilog()
                .UseNServiceBus(hostContext =>
                {
                    var endpoint = EndpointFactory.CreateEndpoint("MyEndpoint");
                    // configure endpoint here
                    endpoint
                        .AddDefaultTransport(hostContext, "RabbitMQ")
                        .ConfigureRouting(routing =>
                            routing.RouteToEndpoint(Assembly.GetExecutingAssembly(), "MyEndpoint"));
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
                        options.AddAspNetCore();
                        // Hooks hosted services into the Generic Host pipeline
                        // while resolving them through Simple Injector
                        options.AddHostedService<Worker>();

                        // Allows injection of ILogger in
                        // application components.
                        options.AddLogging();
                        var container = options.Container;
                        container.AddMediatr(Assembly.GetExecutingAssembly());
                        container.Register<SingletonDependency>(Lifestyle.Singleton);
                        container.Register<ScopedDependency>();
                        container.Register<MyCalculator>();

                        GrpcClientFactory.AllowUnencryptedHttp2 = true;
                        var channel = GrpcChannel.ForAddress("http://localhost:55555");
                        container.Register(() => channel.CreateGrpcService<ICalculator>());
                    });
                    services.AddServiceBusHealthCheck(ctx);
                    services.AddCodeFirstGrpc();
                    services.AddSingleton(typeof(IGrpcServiceActivator<>),
                        typeof(GrpcSimpleInjectorActivator<>));
                })
                .UseConsoleLifetime()
                .Build();

            host.UseSimpleInjector(host.GetContainer());

            // Register application components.
            // probably not doable before asp net has started, and will be called upon first resolution
            //container.Verify();

            return host;
        }
    }
}