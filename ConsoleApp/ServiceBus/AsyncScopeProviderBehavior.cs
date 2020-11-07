using System;
using System.Threading.Tasks;
using NServiceBus.Pipeline;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace ConsoleApp.ServiceBus
{
    public class AsyncScopeProviderBehavior : Behavior<ITransportReceiveContext>
    {
        public AsyncScopeProviderBehavior(Container container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public Container Container => _container;

        #region Base Class Member Overrides

        public override async Task Invoke(ITransportReceiveContext context, Func<Task> next)
        {
            using (AsyncScopedLifestyle.BeginScope(_container))
            {
                await next().ConfigureAwait(false);
            }
        }

        #endregion

        private readonly Container _container;
    }
}