using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using ConsoleApp.Grpc;
using ConsoleApp.Grpc.HealthCheck;
using ConsoleApp.ServiceBus;
using Grpc.AspNetCore.Server;
using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using ProtoBuf.Grpc.Client;
using ProtoBuf.Grpc.Server;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Compact;
using SimpleInjector;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ConsoleApp
{
    public static class Log
    {
        public static ILoggerFactory LoggerFactory { get; } = new SerilogLoggerFactory();

        public static ILogger ForContext<T>()
        {
            return LoggerFactory.CreateLogger<T>();
        }
    }

    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            using var startupLogger = CreateStartupLogger();
            try
            {
                await CreateHost(args).RunAsync();
            }
            catch (Exception exception)
            {
                startupLogger.Fatal(exception, "Host terminated unexpectedly");
                return 1;
            }

            return 0;
        }

        public static Logger CreateStartupLogger()
        {
            var startupLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    new CompactJsonFormatter())
                .CreateLogger();
            Serilog.Log.Logger = startupLogger;
            return startupLogger;
        }

        public static IHealthChecksBuilder AddGrpcClient<TService>(
            this IHealthChecksBuilder builder,
            GrpcChannel channel,
            string name = null,
            HealthStatus? failureStatus = null,
            IEnumerable<string> tags = null,
            TimeSpan? timeout = null) where TService : class
        {
            builder.Services.AddSingleton(
                sp =>
                    new GrpcClientHealthCheck<TService>(channel));
            return builder.Add(new HealthCheckRegistration(name ?? $"Service {typeof(TService).FullName}",
                sp => sp.GetRequiredService<GrpcClientHealthCheck<TService>>(), failureStatus, tags,
                timeout));
        }


        private static IHost CreateHost(string[] args)
        {
            var host = HostBuilderHelper.CreateBuilder<Startup>(args)
                .UseSerilog((hostingContext, services, loggerConfiguration) =>
                {
                    loggerConfiguration = loggerConfiguration
                        .ReadFrom.Configuration(hostingContext.Configuration);
                    if (Debugger.IsAttached) loggerConfiguration.MinimumLevel.Debug().WriteTo.Debug();
                })
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
                    GrpcClientFactory.AllowUnencryptedHttp2 = true;
                    var channel = GrpcChannel.ForAddress("http://localhost:55555",
                        new GrpcChannelOptions {LoggerFactory = Log.LoggerFactory});
                    services.AddSimpleInjector(ctx.GetContainer(), options =>
                    {
                        options.AddAspNetCore();
                        // Hooks hosted services into the Generic Host pipeline
                        // while resolving them through Simple Injector
                        //options.AddHostedService<Worker>();

                        // Allows injection of ILogger in
                        // application components.
                        options.AddLogging();
                        var container = options.Container;
                        container.AddMediatr(Assembly.GetExecutingAssembly());
                        container.Register<SingletonDependency>(Lifestyle.Singleton);
                        container.Register<ScopedDependency>();
                        container.Register<MyCalculator>();
                        container.Register<HealthCheckService<MyCalculator>>();

                        container.Register(() => channel.CreateGrpcService<ICalculator>());
                    });
                    services.AddHealthChecks().AddGrpcClient<ICalculator>(channel);
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