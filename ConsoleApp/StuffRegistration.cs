using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace ConsoleApp
{
    public class StuffRegistration : IContainerRegistrar
    {
        public void AddRegistrations(Container container, HostBuilderContext configuration)
        {
            //throw new NotImplementedException();
        }
    }
}