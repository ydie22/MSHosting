using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace ConsoleApp
{
    public static class HostBuilderHelper
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

        public static IHostBuilder CreateBuilder<TStartup>(string[] args) where TStartup : class
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
                            var section = builderContext.Configuration.GetSection("Kestrel");
                            options.Configure(section);
                        })
                        .ConfigureServices((hostingContext, services) =>
                        {
                            // Host filtering is not included in default config
                            // See https://andrewlock.net/adding-host-filtering-to-kestrel-in-aspnetcore/ to include it
                            // if running an internet-facing endpoint

                            // Forwarded headers only if there is a reverse proxy
                            // See https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-3.1
 
                            services.AddRouting();
                            services.AddHealthChecks();
                        });

                    webBuilder.UseStartup<TStartup>();
                });
        }
    }
}