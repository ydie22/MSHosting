using System;
using MediatR;
using NServiceBus;
using NServiceBus.Features;
using System.Linq;

namespace ConsoleApp.ServiceBus
{
    public static class EndpointFactory
    {
        public static EndpointConfiguration CreateEndpoint(string name)
        {
            var endpoint = new EndpointConfiguration(name);
            endpoint.SendFailedMessagesTo(name + ".Errors");

            endpoint.DisableFeature<TimeoutManager>();

            // ensure the process will have write rights to the diagnostics directory
            endpoint.SetDiagnosticsPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            // Defining conventions for message types resolution
            //endpoint.Conventions()
            //    .DefiningEventsAs(
            //        type => type.Namespace != null && type.Namespace.Contains("Contracts") && type.Name.EndsWith("Event")
            //                && type.GetInterfaces().Contains(typeof(INotification)))
            //    // tentative of command definition: excludes commands expecting a response (thus one-way only)
            //    .DefiningCommandsAs(
            //        type => type.Namespace != null && type.Namespace.Contains("Contracts") && type.Name.EndsWith("Request")
            //                && type.GetInterfaces().Contains(typeof(IRequest)));
            return endpoint;
        }
    }
}