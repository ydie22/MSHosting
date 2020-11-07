using System;
using System.Threading.Tasks;
using MediatR;
using NServiceBus;
using SimpleInjector;

namespace ConsoleApp.ServiceBus
{
    public class MediatedRequestMessageHandler<T> : IHandleMessages<T> where T : class, IRequest
    {
        public MediatedRequestMessageHandler(IMediator mediator, Container container)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _container = container;
        }

        public IMediator Mediator => _mediator;

        #region IHandleMessages<T> Members

        public async Task Handle(T message, IMessageHandlerContext context)
        {
            //var session = _container.GetInstance<IMessageSession>();
            await _mediator.Send(message).ConfigureAwait(false);
        }

        #endregion

        private readonly IMediator _mediator;
        private readonly Container _container;
    }
}