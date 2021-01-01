using Microsoft.Extensions.Logging;

namespace ConsoleApp
{
    public class SingletonDependency
    {
        private static readonly ILogger log = Log.ForContext<SingletonDependency>();
    }

    public class ScopedDependency
    {
    }
}