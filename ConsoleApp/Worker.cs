using System;
using System.Threading;
using System.Threading.Tasks;
using ConsoleApp.Grpc;
using Microsoft.Extensions.Hosting;
using NServiceBus;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace ConsoleApp
{
    public class Worker : BackgroundService, IAsyncDisposable
    {
        private readonly Container _container;
        private readonly SingletonDependency _singletonDependency;

        public Worker(SingletonDependency singletonDependency, Container container)
        {
            _singletonDependency = singletonDependency ?? throw new ArgumentNullException(nameof(singletonDependency));
            _container = container;
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
                    var calculator = _container.GetInstance<ICalculator>();
                    var result = await calculator.MultiplyAsync(new MultiplyRequest {X = 12, Y = 4});
                }

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}