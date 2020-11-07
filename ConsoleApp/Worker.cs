using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace ConsoleApp
{
    public class Worker : BackgroundService, IAsyncDisposable
    {
        private readonly Container _container;
        private readonly WorkerDependency _dependency;
        private readonly ILogger _logger;

        public Worker(WorkerDependency dependency, Container container, ILogger logger)
        {
            _dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
            _container = container;
            _logger = logger;
            _logger.LogInformation("Injected!");
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using (AsyncScopedLifestyle.BeginScope(_container))
                {
                    var session = _container.GetInstance<IMessageSession>();
                    await session.Send(new SomeCommand {Property = "somevalue"}, new SendOptions());
                }

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}