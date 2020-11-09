using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using ConsoleApp.ServiceBus;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using NServiceBus;
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
            var host = CreateBuilder(args)
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

                        // Allows injection of ILogger
                        // application components.
                        options.AddLogging();
                        var container = options.Container;
                        container.AddMediatr(Assembly.GetExecutingAssembly());
                        container.Register<WorkerDependency>(Lifestyle.Singleton);
                    });
                    services.AddHealthChecks()
                        .AddRabbitMQ(rabbitConnectionString: "amqp://guest:guest@localhost/", name: "rabbitmq");

                })
                .UseConsoleLifetime()
                .Build();

            host.UseSimpleInjector(host.GetContainer());

            // Register application components.
            // probably not doable before asp net has started, and will be called upon first resolution
            //container.Verify();

            return host;
        }

        private static IHostBuilder CreateBuilder(string[] args)
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
                    // load environment?
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
                })
                .ConfigureWebHost(webBuilder =>
                {
                    //webBuilder.ConfigureAppConfiguration((ctx, cb) =>
                    //{
                    //    if (ctx.HostingEnvironment.IsDevelopment())
                    //        StaticWebAssetsLoader.UseStaticWebAssets(ctx.HostingEnvironment, ctx.Configuration);
                    //});
                    webBuilder.UseKestrel((builderContext, options) =>
                        {
                            options.Configure(builderContext.Configuration.GetSection("Kestrel"));
                        })
                        .ConfigureServices((hostingContext, services) =>
                        {
                            // Host filtering is not included in default config
                            // See https://andrewlock.net/adding-host-filtering-to-kestrel-in-aspnetcore/ to include it
                            // if running an internet-facing endpoint

                            // Forwarded headers only if there is a reverse proxy
                            // See https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-3.1
                            //if (string.Equals("true", hostingContext.Configuration["ForwardedHeaders_Enabled"],
                            //    StringComparison.OrdinalIgnoreCase))
                            //{
                            //    services.Configure<ForwardedHeadersOptions>(options =>
                            //    {
                            //        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                            //                                   ForwardedHeaders.XForwardedProto;
                            //        // Only loopback proxies are allowed by default. Clear that restriction because forwarders are
                            //        // being enabled by explicit configuration.
                            //        options.KnownNetworks.Clear();
                            //        options.KnownProxies.Clear();
                            //    });

                            //    services.AddTransient<IStartupFilter, ForwardedHeadersStartupFilter>();
                            //}

                            services.AddRouting();
                            services.AddHealthChecks();
                        });

                    webBuilder.UseStartup<Startup>();
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