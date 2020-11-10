using System;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Unicast;
using SimpleInjector;

namespace ConsoleApp.ServiceBus
{
    public static class EndpointExtensions
    {
        // Move this into builder context properties
        private static bool _commandHandlerRegistered;

        public static EndpointConfiguration HandleCommand<TCommand>(this EndpointConfiguration endpointConfiguration,
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

            return endpointConfiguration;
        }

        public static TransportExtensions<RabbitMQTransport> AddDefaultTransport(this EndpointConfiguration endpoint,
            HostBuilderContext hostBuilderContext, string connectionStringName)
        {
            var transport = endpoint.UseTransport<RabbitMQTransport>()
                // Configuring transport and serialization
                .ConnectionString(hostBuilderContext.Configuration.GetConnectionString(connectionStringName))
                .Transactions(TransportTransactionMode.ReceiveOnly);

            // TODO: use custom RuntimeTypeModel in serializer config to ensure that surrogates are taken into account (breaking change!)
            //endpointConfiguration.UseSerialization<ProtoBufSerializer>();
            hostBuilderContext.Properties[Constants.BusConnectionStringNameKey] = connectionStringName;

            return transport;
        }

        public static TransportExtensions<RabbitMQTransport> ConfigureRouting(
            this TransportExtensions<RabbitMQTransport> transport,
            Action<RoutingSettings<RabbitMQTransport>> configure)
        {
            transport.UseConventionalRoutingTopology();
            configure(transport.Routing());
            return transport;
        }
    }

    internal static class Constants
    {
        internal const string BusConnectionStringNameKey = "ServiceBus.ConnectionStringName";
    }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceBusHealthCheck(this IServiceCollection services, HostBuilderContext ctx)
        {
            var connectionStringName = (string)ctx.Properties[Constants.BusConnectionStringNameKey];
            services.AddHealthChecks().AddRabbitMQ(ctx.Configuration.GetConnectionString(connectionStringName), name: "RabbitMQ");
            return services;
        }
    }
}