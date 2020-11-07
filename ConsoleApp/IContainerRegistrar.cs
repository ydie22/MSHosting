using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace ConsoleApp
{
    public interface IContainerRegistrar
    {
        void AddRegistrations(Container container, HostBuilderContext configuration);
    }
}