using System;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Transport;
using NServiceBus.Unicast;
using SimpleInjector;

namespace ConsoleApp.ServiceBus
{
    public static class EndpointExtensions
    {
        // MOve this into builder context properties
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

            return transport;
        }

        public static TransportExtensions<RabbitMQTransport> ConfigureRouting(this TransportExtensions<RabbitMQTransport> transport,
            Action<RoutingSettings<RabbitMQTransport>> configure)
        {
            transport.UseConventionalRoutingTopology();
            configure(transport.Routing());
            return transport;
        }
    }
}