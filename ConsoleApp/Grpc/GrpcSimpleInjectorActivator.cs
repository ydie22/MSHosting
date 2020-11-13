using System;
using System.Threading.Tasks;
using Grpc.AspNetCore.Server;
using SimpleInjector;

namespace ConsoleApp.Grpc
{
    public class GrpcSimpleInjectorActivator<T> : IGrpcServiceActivator<T>
        where T : class
    {
        private readonly Container _container;

        public GrpcSimpleInjectorActivator(Container container)
        {
            _container = container;
        }

        public GrpcActivatorHandle<T> Create(IServiceProvider serviceProvider)
        {
            return new GrpcActivatorHandle<T>(_container.GetInstance<T>(), false, null);
        }

        public ValueTask ReleaseAsync(GrpcActivatorHandle<T> service)
        {
            return default;
        }
    }
}