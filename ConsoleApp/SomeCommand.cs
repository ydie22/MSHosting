using MediatR;
using NServiceBus;

namespace ConsoleApp
{
    public class SomeCommand : ICommand, IRequest
    {
        public string Property { get; set; }
    }
}