using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using NServiceBus;
using SimpleInjector;

namespace ConsoleApp
{
    public class SomeCommandHandler : IRequestHandler<SomeCommand>
    {
        private readonly ILogger _logger;
        private readonly Container _container;

        public SomeCommandHandler(ILogger logger, Container container)
        {
            _logger = logger;
            _container = container;
        }

        public async Task<Unit> Handle(SomeCommand request, CancellationToken cancellationToken)
        {
            //var session = _container.GetInstance<IMessageSession>();
            //await session.Send(new SomeCommand());
            _logger.LogInformation("Message handled.");
            return Unit.Value;
        }
    }
}